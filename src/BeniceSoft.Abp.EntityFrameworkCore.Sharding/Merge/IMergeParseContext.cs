using BeniceSoft.Core;
using BeniceSoft.Core.Strategy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Collections.Concurrent;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IMergeParseContext
{
    int? Skip { get; }

    int? Take { get; }
}

internal sealed class StreamMergeContext : IMergeParseContext, IDisposable, IAsyncDisposable
{
    private readonly IRouteTailFactory _routeTailFactory;
    private readonly ITrackerManager _trackerManager;
    private readonly ShardingOptions _shardingConfigOptions;
    private readonly ConcurrentDictionary<DbContext, object?> _parallel;

    public StreamMergeContext(IMergeQueryCompilerContext mergeContext, IParseResult parseResult, IRewriteResult rewriteResult, IOptimizeResult optimizeResult)
    {
        MergeContext = mergeContext;
        ParseResult = parseResult;
        RewriteQueryable = rewriteResult.RewriteQueryable;
        OptimizeResult = optimizeResult;
        RuntimeContext = ((DbContext)mergeContext.DbContext).GetRuntimeContext();
        _routeTailFactory = RuntimeContext.RouteTailFactory;
        _trackerManager = RuntimeContext.TrackerManager;
        _shardingConfigOptions = RuntimeContext.Options;
        Entities = MergeContext.Entities.Keys.ToHashSet();
        _parallel = new ConcurrentDictionary<DbContext, object?>(Environment.ProcessorCount, mergeContext.RouteResult.RouteUnits.Count);
        Sorts = [.. parseResult.OrderByContext.Sorts];
        Skip = parseResult.PagedContext.Skip;
        Take = parseResult.PagedContext.Take;
    }

    public IMergeQueryCompilerContext MergeContext { get; }

    public IShardingRuntimeContext RuntimeContext { get; }

    public IParseResult ParseResult { get; }

    public IQueryable RewriteQueryable { get; }

    public IOptimizeResult OptimizeResult { get; }

    public PropertySorting[] Sorts { get; set; }

    public int? Skip { get; set; }

    public int? Take { get; set; }

    public SelectContext SelectContext => ParseResult.SelectContext;

    public GroupByContext GroupByContext => ParseResult.GroupByContext;

    public ShardingRouteResult RouteResult => MergeContext.RouteResult;

    /// <summary>
    /// 本次查询涉及的对象
    /// </summary>
    public ISet<Type> Entities { get; }

    /// <summary>
    /// 本次查询跨库
    /// </summary>
    public bool IsCrossDataSource => MergeContext.IsCrossDataSource;

    /// <summary>
    /// 本次查询跨表
    /// </summary>
    public bool IsCrossTable => MergeContext.IsCrossTable;

    public IComparer<string> TailComparer => OptimizeResult.TailComparer;

    /// <summary>
    /// 分表后缀比较是否重排正序
    /// </summary>
    public bool SameTailComparer => OptimizeResult.SameTailComparer;

    public IQueryable OriginalQueryable => MergeContext.Result.Queryable;

    public int? PagedTake
    {
        get
        {
            if (Take.HasValue)
            {
                return Skip.GetValueOrDefault() + Take.Value;
            }

            return default;
        }
    }

    public bool PagedQuery => Skip is > 0 || Take is > 0;

    public bool HasGroup => GroupByContext.Expression != null;

    public bool GroupMemoryMerge => HasGroup && GroupByContext.MemoryMerge;

    public bool Merge => IsCrossDataSource || IsCrossTable;

    public bool Single => Entities.Where(MergeContext.Manager.IsSharding).Take(2).Count() == 1;

    public Type SingleType => Entities.Single(MergeContext.Manager.IsSharding);

    public IShardingDbContext DbContext => MergeContext.DbContext;

    public int MaxQueryConnections => OptimizeResult.MaxQueryConnections;

    public ConnectionMode ConnectionMode => OptimizeResult.ConnectionMode;

    public bool IsParallel => MergeContext.IsParallel;

    public bool NoTracking => MergeContext.NoTracking;

    public IShardingComparer Comparer => ((DbContext)DbContext).GetRuntimeContext().Comparer;

    /// <summary>
    /// 是否无路由匹配
    /// </summary>
    public bool RouteNotMatch => RouteResult.IsEmpty;

    public bool UseMerge => MergeContext.UseMerge;

    public bool Sequence => OptimizeResult.Sequence;

    public bool UseTrack(Type entityType)
    {
        if (!IsParallel)
        {
            return false;
        }

        return NoTracking && _trackerManager.UseTrack(entityType);
    }

    public ConnectionMode GetConnectionMode(int sqlCount)
    {
        switch (OptimizeResult.ConnectionMode)
        {
            case ConnectionMode.MemoryStrictly:
            case ConnectionMode.ConnectionStrictly:
                return OptimizeResult.ConnectionMode;
            default:
                {
                    return MaxQueryConnections < sqlCount ? ConnectionMode.ConnectionStrictly : ConnectionMode.MemoryStrictly;
                }
        }
    }

    public void ReverseSorting()
    {
        if (Sorts.IsNotNull())
        {
            // Last/Reverse 依赖方向翻转；原先原样拷贝会导致 Last 实际返回 First
            var sorts = Sorts.Select(o => new PropertySorting(
                o.Expression,
                o.Direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending,
                o.OwnerType)).ToArray();
            Sorts = sorts;
        }
    }

    /// <summary>
    /// 创建对应的dbcontext
    /// </summary>
    /// <param name="sqlRouteUnit">数据库路由最小单元</param>
    /// <returns></returns>
    public DbContext CreateDbContext(ISqlRouteUnit sqlRouteUnit)
    {
        var routeTail = _routeTailFactory.Create(sqlRouteUnit.RouteResult);

        var ctx = DbContext.GetExecutor().Create(CreateDbStrategy.ParallelQuery, sqlRouteUnit.DataSource, routeTail);
        _parallel.TryAdd(ctx, null);

        return ctx;
    }

    /// <summary>
    /// 如果返回false那么就说明不需要继续查询了
    /// 返回true表示需要继续查询
    /// </summary>
    /// <param name="emptyFactory"></param>
    /// <param name="result"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="ShardingNotMatchException"></exception>
    public bool TryPrepareExecute<T>(Func<T> emptyFactory, out T result)
    {
        if (Take == 0)
        {
            result = emptyFactory();
            return false;
        }

        if (RouteNotMatch)
        {
            if (_shardingConfigOptions.ThrowRouteNotMatch)
            {
                throw new ShardingNotMatchException(MergeContext.Expression.Print());
            }
            else
            {
                result = emptyFactory();
                return false;
            }
        }

        result = default!;
        return true;
    }

    public async ValueTask<bool> DisposeAsync(DbContext? db)
    {
        if (db is null)
        {
            return false;
        }

        if (_parallel.TryRemove(db, out var _))
        {
            await db.DisposeAsync();
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        foreach (var db in _parallel.Keys)
        {
            db.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var db in _parallel.Keys)
        {
            await db.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}

public interface IMergeQueryCompiler
{
}

public interface IMergeQuerySqlGeneratorFactory
{
}

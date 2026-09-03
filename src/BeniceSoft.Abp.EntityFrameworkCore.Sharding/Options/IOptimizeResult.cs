using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 优化结果
/// </summary>
internal interface IOptimizeResult
{
    int MaxQueryConnections { get; }

    ConnectionMode ConnectionMode { get; }

    bool Sequence { get; }

    bool SameTailComparer { get; }

    IComparer<string> TailComparer { get; }
}

internal sealed class OptimizeResult(int maxQueryConnections, ConnectionMode connectionMode, bool sequence, bool sameTailComparer, IComparer<string> tailComparer) : IOptimizeResult
{
    public int MaxQueryConnections { get; } = maxQueryConnections;

    public ConnectionMode ConnectionMode { get; } = connectionMode;

    public bool Sequence { get; } = sequence;

    public bool SameTailComparer { get; } = sameTailComparer;

    public IComparer<string> TailComparer { get; } = tailComparer;
}

internal interface IRewriteResult
{
    /// <summary>
    /// 最原始的表达式
    /// </summary>
    /// <returns></returns>
    IQueryable CombineQueryable { get; }

    /// <summary>
    /// 被重写后的表达式
    /// </summary>
    /// <returns></returns>
    IQueryable RewriteQueryable { get; }
}

internal sealed class RewriteResult(IQueryable combineQueryable, IQueryable rewriteQueryable) : IRewriteResult
{
    public IQueryable CombineQueryable { get; } = combineQueryable;

    public IQueryable RewriteQueryable { get; } = rewriteQueryable;
}

internal interface IParseResult
{
    PagedContext PagedContext { get; }

    SelectContext SelectContext { get; }

    OrderByContext OrderByContext { get; }

    GroupByContext GroupByContext { get; }
}

internal sealed class ParseResult(PagedContext pagedContext, SelectContext selectContext, OrderByContext orderByContext, GroupByContext groupByContext) : IParseResult
{
    public PagedContext PagedContext { get; } = pagedContext;

    public SelectContext SelectContext { get; } = selectContext;

    public OrderByContext OrderByContext { get; } = orderByContext;

    public GroupByContext GroupByContext { get; } = groupByContext;
}

internal interface IPrepareParseResult
{
    /// <summary>
    /// 获取当前分片上下文
    /// </summary>
    /// <returns></returns>
    IShardingDbContext Context { get; }

    /// <summary>
    /// 获取原始的查询表达式
    /// </summary>
    /// <returns></returns>
    Expression Expression { get; }

    /// <summary>
    /// 是否使用union all 聚合
    /// </summary>
    /// <returns></returns>
    bool UseMerge { get; }

    /// <summary>
    /// 当前查询的连接数限制
    /// </summary>
    /// <returns></returns>
    int MaxQueryConnections { get; }

    /// <summary>
    /// 当前查询的连接模式
    /// </summary>
    /// <returns></returns>
    ConnectionMode ConnectionMode { get; }

    /// <summary>
    /// 在启用读写分离后如果设置了readonly那么就走readonly否则为null
    /// </summary>
    /// <returns></returns>
    bool ReadOnly { get; }

    /// <summary>
    /// 自定义路由
    /// </summary>
    /// <returns></returns>
    Action<ShardingRouteContext>? RouteFactory { get; }

    bool Sequence { get; }

    bool SameComparer { get; }

    Dictionary<Type, IQueryable?> Entities { get; }

    bool NoTracking { get; }

    bool IgnoreFilter { get; }
}

internal sealed class PrepareParseResult : IPrepareParseResult
{
    public PrepareParseResult(IShardingDbContext context, Expression expression, ShardingPrepareResult result)
    {
        Context = context;
        Expression = expression;

        RouteFactory = result.RouteOptions?.RouteFactory;
        UseMerge = result.UseMerge;
        MaxQueryConnections = result.ConnectionOptions?.MaxQueryConnections ?? 0;
        ConnectionMode = result.ConnectionOptions?.ConnectionMode ?? ConnectionMode.Automatic;

        if (context.GetExecutor().GetVirtualDataSource().UseSeparation)
        {
            ReadOnly = result.SeparationOptions?.ReadOnly ?? false;
        }

        Sequence = result.SequenceOptions?.Sequence ?? false;
        SameComparer = result.SequenceOptions?.SameComparer ?? false;
        Entities = result.Entities;
        NoTracking = result.NoTracking;
        IgnoreFilter = result.IgnoreFilter;
    }

    public IShardingDbContext Context { get; }

    public Expression Expression { get; }

    public bool UseMerge { get; }

    public int MaxQueryConnections { get; }

    public ConnectionMode ConnectionMode { get; }

    public bool ReadOnly { get; }

    public Action<ShardingRouteContext>? RouteFactory { get; }

    public bool Sequence { get; }

    public bool SameComparer { get; }

    public Dictionary<Type, IQueryable?> Entities { get; }

    public bool NoTracking { get; }

    public bool IgnoreFilter { get; }
}

internal sealed class ShardingPrepareResult(ShardingAsConnectionOptions? connectionOptions, ShardingAsRouteOptions? routeOptions, ShardingAsSeparationOptions? separationOptions, ShardingAsSequenceOptions? sequenceOptions, bool useMerge, Dictionary<Type, IQueryable?> entities, bool noTracking, bool ignoreFilter)
{
    public ShardingAsConnectionOptions? ConnectionOptions { get; } = connectionOptions;

    public ShardingAsRouteOptions? RouteOptions { get; } = routeOptions;

    public ShardingAsSeparationOptions? SeparationOptions { get; } = separationOptions;

    public ShardingAsSequenceOptions? SequenceOptions { get; } = sequenceOptions;

    public bool UseMerge { get; } = useMerge;

    public Dictionary<Type, IQueryable?> Entities { get; } = entities;

    public bool NoTracking { get; } = noTracking;

    public bool IgnoreFilter { get; } = ignoreFilter;
}

internal interface IPrepareParser
{
    IPrepareParseResult Parse(IShardingDbContext context, Expression query);
}

internal sealed class PrepareParser : IPrepareParser
{
    public IPrepareParseResult Parse(IShardingDbContext context, Expression query)
    {
        var visitor = new ShardingPrepareVisitor(context);
        var expression = visitor.Visit(query);
        var result = visitor.GetShardingPrepareResult();
        return new PrepareParseResult(context, expression, result);
    }
}

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IQueryCompilerContext
{
    Type DbContextType { get; }

    IShardingDbContext DbContext { get; }

    QueryCompilerExecutor? Executor { get; }

    IEntityMetadataManager Manager { get; }

    Dictionary<Type, IQueryable?> Entities { get; }

    Expression Expression { get; }

    string Name { get; }

    bool IsEnumerable { get; }

    /// <summary>
    /// 当前是否读写分离走读库(包括是否启用读写分离和是否当前的DbContext启用了读库查询)
    /// </summary>
    /// <returns></returns>
    bool IsParallel { get; }

    /// <summary>
    /// 是否是未追踪查询
    /// </summary>
    /// <returns></returns>
    bool NoTracking { get; }

    bool UseMerge { get; }

    int MaxQueryConnections { get; }

    ConnectionMode ConnectionMode { get; }

    bool Sequence { get; }

    bool SameComparer { get; }

    bool Single { get; }

    Type? SingleType { get; }

    internal bool IsEntityQuery()
    {
        if (Expression is MethodCallExpression callExpression)
        {
            var name = callExpression.Method.Name;
            switch (name)
            {
                case nameof(Queryable.First):
                case nameof(Queryable.FirstOrDefault):
                case nameof(Queryable.Last):
                case nameof(Queryable.LastOrDefault):
                case nameof(Queryable.Single):
                case nameof(Queryable.SingleOrDefault):
                    return true;
            }
        }

        return false;
    }
}

internal sealed class QueryCompilerContext : IQueryCompilerContext
{
    internal const string Enumerable = "Enumerable";

    public QueryCompilerContext(IPrepareParseResult result)
    {
        DbContext = result.Context;
        DbContextType = DbContext.GetType();
        var db = DbContext as DbContext
            ?? throw new ShardingInvalidOperationException("Query compiler context requires a DbContext.");
        var ctx = db.GetRuntimeContext();
        Expression = result.Expression;
        Entities = result.Entities;
        NoTracking = GetNoTracking(db, result.NoTracking);
        UseMerge = result.UseMerge;
        MaxQueryConnections = result.MaxQueryConnections;
        ConnectionMode = result.ConnectionMode;
        Manager = ctx.EntityMetadataManager;
        IsParallel = result.ReadOnly;
        Sequence = result.Sequence;
        SameComparer = result.SameComparer;
        Name = GetName(Expression, out var enumerable);
        IsEnumerable = enumerable;
        Single = Entities.Keys.Where(Manager.IsSharding).Take(2).Count() == 1;
        if (Single)
        {
            SingleType = Entities.Keys.Single(Manager.IsSharding);
        }

        Executor = GetQueryCompilerExecutor(ctx);
    }

    private static string GetName(Expression expression, out bool enumerable)
    {
        enumerable = false;
        var isEnumerableQuery = expression.Type.HasImplemented(typeof(IQueryable<>));
        if (isEnumerableQuery)
        {
            enumerable = true;
            return Enumerable;
        }

        if (expression is MethodCallExpression methodCallExpression)
        {
            return methodCallExpression.Method.Name;
        }
        else
        {
            throw new ShardingInvalidOperationException($"queryable:[{expression.Print()}] not {nameof(MethodCallExpression)} cant found query method name");
        }
    }

    private static bool GetNoTracking(DbContext db, bool track)
    {
        if (!db.ChangeTracker.AutoDetectChangesEnabled)
        {
            return false;
        }

        if (!track)
        {
            return false;
        }

        return db.ChangeTracker.QueryTrackingBehavior == QueryTrackingBehavior.TrackAll;
    }

    public QueryCompilerExecutor? GetQueryCompilerExecutor(IShardingRuntimeContext ctx)
    {
        var has = Entities.Keys.All(o => !Manager.IsSharding(o));
        if (!has)
        {
            return null;
        }

        var data = ctx.VirtualDataSource;
        var factory = ctx.RouteTailFactory;
        var strategy = !IsParallel ? CreateDbStrategy.Share : CreateDbStrategy.ParallelQuery;
        var db = DbContext.GetExecutor().Create(strategy, data.DefaultDataSource, factory.Create(string.Empty));
        return new QueryCompilerExecutor(db, Expression);
    }

    public Type DbContextType { get; }

    public IShardingDbContext DbContext { get; }

    public QueryCompilerExecutor? Executor { get; }

    public IEntityMetadataManager Manager { get; }

    public Dictionary<Type, IQueryable?> Entities { get; }

    public Expression Expression { get; }

    public string Name { get; }

    public bool IsEnumerable { get; }

    public bool IsParallel { get; }

    public bool NoTracking { get; }

    public bool UseMerge { get; }

    public int MaxQueryConnections { get; }

    public ConnectionMode ConnectionMode { get; }

    public bool Sequence { get; }

    public bool SameComparer { get; }

    public bool Single { get; }

    public Type? SingleType { get; }
}

internal interface IQueryCompilerContextFactory
{
    IQueryCompilerContext Create(IPrepareParseResult result);
}

internal sealed class QueryCompilerContextFactory : IQueryCompilerContextFactory
{
    private readonly IDataSourceRouteRuleFactory _dataSourceFactory;
    private readonly ITableRouteRuleFactory _tableRouteFactory;

    private readonly ILogger<QueryCompilerContextFactory> _logger;
    private readonly bool _logDebug;
    private static readonly IQueryableCombine _enumerableQueryableCombine;
    private static readonly IQueryableCombine _allQueryableCombine;
    private static readonly IQueryableCombine _constantQueryableCombine;
    private static readonly IQueryableCombine _selectQueryableCombine;
    private static readonly IQueryableCombine _whereQueryableCombine;
    private static readonly IQueryableCombine _updateQueryableCombine;
    private static readonly IQueryableCombine _deleteQueryableCombine;

    static QueryCompilerContextFactory()
    {
        _enumerableQueryableCombine = new EnumerableQueryableCombine();
        _allQueryableCombine = new AllQueryableCombine();
        _constantQueryableCombine = new ConstantQueryableCombine();
        _selectQueryableCombine = new SelectQueryableCombine();
        _whereQueryableCombine = new WhereQueryableCombine();
        _updateQueryableCombine = new UpdateQueryableCombine();
        _deleteQueryableCombine = new QueryableCombine();
    }

    public QueryCompilerContextFactory(IDataSourceRouteRuleFactory dataSourceFactory, ITableRouteRuleFactory tableRouteFactory, ILogger<QueryCompilerContextFactory> logger)
    {
        _dataSourceFactory = dataSourceFactory;
        _tableRouteFactory = tableRouteFactory;
        _logger = logger;
        _logDebug = _logger.IsEnabled(LogLevel.Debug);
    }

    public IQueryCompilerContext Create(IPrepareParseResult result)
    {
        var ctx = new QueryCompilerContext(result);
        if (ctx.Executor is not null)
        {
            if (_logDebug)
            {
                _logger.LogDebug($"{ctx.Expression.Print()} is native query");
            }

            return ctx;
        }

        var queryableCombine = GetQueryableCombine(ctx);
        if (_logDebug)
        {
            _logger.LogDebug($"queryable combine:{queryableCombine.GetType()}");
            _logger.LogDebug($"queryable combine before:{ctx.Expression.Print()}");
        }

        var combineResult = queryableCombine.Combine(ctx);
        if (_logDebug)
        {
            _logger.LogDebug($"queryable combine after:{combineResult.Queryable}");
        }

        var dataSourceRouteResult = _dataSourceFactory.Route(combineResult.Queryable, result.Context, result.Entities);
        if (_logDebug)
        {
            _logger.LogDebug($"{dataSourceRouteResult}");
        }

        var routeResult = _tableRouteFactory.Route(dataSourceRouteResult, combineResult.Queryable, result.Entities);
        if (_logDebug)
        {
            _logger.LogDebug($"table route results:{routeResult}");
        }

        var mergeContext = new MergeQueryCompilerContext(ctx, combineResult, routeResult);
        return mergeContext;
    }

    private IQueryableCombine GetQueryableCombine(QueryCompilerContext queryCompilerContext)
    {
        if (queryCompilerContext.IsEnumerable)
        {
            return _enumerableQueryableCombine;
        }
        else
        {
            return GetMethodQueryableCombine(queryCompilerContext);
        }
    }

    private IQueryableCombine GetMethodQueryableCombine(QueryCompilerContext queryCompilerContext)
    {
        string? methodName = null;
        if (queryCompilerContext.Expression is MethodCallExpression methodCallExpression)
        {
            methodName = methodCallExpression.Method.Name;
            switch (methodName)
            {
                case nameof(Queryable.First):
                case nameof(Queryable.FirstOrDefault):
                case nameof(Queryable.Last):
                case nameof(Queryable.LastOrDefault):
                case nameof(Queryable.Single):
                case nameof(Queryable.SingleOrDefault):
                case nameof(Queryable.Count):
                case nameof(Queryable.LongCount):
                case nameof(Queryable.Any):
                    return _whereQueryableCombine;
                case nameof(EntityFrameworkQueryableExtensions.ExecuteUpdate):
                    return _updateQueryableCombine;
                case nameof(EntityFrameworkQueryableExtensions.ExecuteDelete):
                    return _deleteQueryableCombine;
                case nameof(Queryable.All):
                    return _allQueryableCombine;
                case nameof(Queryable.Max):
                case nameof(Queryable.Min):
                case nameof(Queryable.Sum):
                case nameof(Queryable.Average):
                    return _selectQueryableCombine;
                case nameof(Queryable.Contains):
                    return _constantQueryableCombine;
            }
        }

        throw new ShardingException($"query expression:[{methodName}] is not terminate operate");
    }
}

using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IMergeQueryCompilerContext : IQueryCompilerContext
{
    QueryCombineResult Result { get; }

    ShardingRouteResult RouteResult { get; }

    bool IsCrossTable { get; }

    bool IsCrossDataSource { get; }

    int? FixedTake { get; }
}

internal sealed class MergeQueryCompilerContext : IMergeQueryCompilerContext
{
    public MergeQueryCompilerContext(IQueryCompilerContext context, QueryCombineResult combineResult, ShardingRouteResult routeResult)
    {
        DbContext = context.DbContext;
        var ctx = ((DbContext)DbContext).GetRuntimeContext();
        Result = combineResult;
        RouteResult = routeResult;
        IsCrossTable = RouteResult.IsCrossTable;
        IsCrossDataSource = RouteResult.IsCrossDataSource;
        FixedTake = GetFixedTake(context.Name);
        DbContextType = context.DbContextType;
        Single = RouteResult.RouteUnits.Count == 1;
        IsParallel = IsCrossTable || context.IsParallel || RouteResult.ExistCrossTableTails;
        Expression = context.Expression;
        Executor = GetQueryCompilerExecutor(ctx);
        Manager = context.Manager;
        Entities = context.Entities;
        Name = context.Name;
        IsEnumerable = context.IsEnumerable;
        NoTracking = context.NoTracking;
        UseMerge = context.UseMerge;
        MaxQueryConnections = context.MaxQueryConnections;
        ConnectionMode = context.ConnectionMode;
        Sequence = context.Sequence;
        SingleType = context.SingleType;
        SameComparer = context.SameComparer;
    }

    private int? GetFixedTake(string name)
    {
        return name switch
        {
            nameof(Enumerable.First) or nameof(Enumerable.FirstOrDefault) => 1,
            nameof(Enumerable.Single) or nameof(Enumerable.SingleOrDefault) => 2,
            nameof(Enumerable.Last) or nameof(Enumerable.LastOrDefault) => 1,
            _ => null,
        };
    }

    public QueryCompilerExecutor? GetQueryCompilerExecutor(IShardingRuntimeContext context)
    {
        if (RouteResult.IsEmpty)
        {
            return null;
        }

        if (!Single)
        {
            return null;
        }

        var routeTailFactory = context.RouteTailFactory;
        var sqlRouteUnit = RouteResult.RouteUnits[0];
        var strategy = !IsParallel ? CreateDbStrategy.Share : CreateDbStrategy.ParallelQuery;

        var ctx = DbContext.GetExecutor().Create(strategy, sqlRouteUnit.DataSource, routeTailFactory.Create(sqlRouteUnit.RouteResult));
        return new QueryCompilerExecutor(ctx, Expression);
    }

    public QueryCombineResult Result { get; }

    public ShardingRouteResult RouteResult { get; }

    public bool IsCrossTable { get; }

    public bool IsCrossDataSource { get; }

    public int? FixedTake { get; }

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

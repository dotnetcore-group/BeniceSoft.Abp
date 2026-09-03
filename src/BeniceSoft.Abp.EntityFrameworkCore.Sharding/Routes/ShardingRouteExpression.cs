using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 高性能路由条件组合委托
/// 无需compile支持路由条件直接组合and 和 or
/// </summary>
internal sealed class ShardingRouteExpression(Func<string, bool> routePredicate)
{
    private static readonly Func<string, bool> _truePredicate = tail => true;
    private static readonly Func<string, bool> _falsePredicate = tail => false;
    private readonly Func<string, bool> _routePredicate = routePredicate ?? throw new ArgumentNullException(nameof(routePredicate));

    /// <summary>
    /// 默认创建一个true委托
    /// </summary>
    public static ShardingRouteExpression True => new();
    /// <summary>
    /// 默认创建一个false委托
    /// </summary>
    public static ShardingRouteExpression False => new(_falsePredicate);

    public ShardingRouteExpression() : this(_truePredicate)
    {
    }

    /// <summary>
    /// and链接当前委托和外部传入的委托
    /// </summary>
    /// <param name="routePredicateExpression"></param>
    /// <returns></returns>
    public ShardingRouteExpression And(ShardingRouteExpression routePredicateExpression)
    {
        var routePredicate = routePredicateExpression.GetRoutePredicate();

        bool Expr(string tail)
        {
            return _routePredicate(tail) && routePredicate(tail);
        }

        return new ShardingRouteExpression(Expr);
    }
    /// <summary>
    /// or链接当前委托和外部传入的委托
    /// </summary>
    /// <param name="routePredicateExpression"></param>
    /// <returns></returns>
    public ShardingRouteExpression Or(ShardingRouteExpression routePredicateExpression)
    {
        var routePredicate = routePredicateExpression.GetRoutePredicate();
        bool Expr(string tail)
        {
            return _routePredicate(tail) || routePredicate(tail);
        }

        return new ShardingRouteExpression(Expr);
    }

    /// <summary>
    /// 返回当前表达式的路由委托条件
    /// </summary>
    /// <returns></returns>
    public Func<string, bool> GetRoutePredicate()
    {
        return _routePredicate;
    }
}

public sealed class ShardingDataSourceRoute(IQueryable? queryable = null, object? dataSource = null, object? keyValue = null, Expression? predicate = null)
{
    public IQueryable? GetQueryable()
    {
        return queryable;
    }
    public object? GetShardingKeyValue()
    {
        return keyValue;
    }

    public object? GetShardingDataSource()
    {
        return dataSource;
    }

    public Expression? GetPredicate()
    {
        return predicate;
    }

    public bool UseQueryable()
    {
        return queryable != null;
    }

    public bool UseValue()
    {
        return keyValue != null;
    }

    public bool UseEntity()
    {
        return dataSource != null;
    }

    public bool UsePredicate()
    {
        return predicate != null;
    }
}

public sealed class ShardingTableRoute(IQueryable? queryable = null, object? table = null, object? keyValue = null, Expression? predicate = null)
{
    public IQueryable? GetQueryable()
    {
        return queryable;
    }
    public object? GetShardingKeyValue()
    {
        return keyValue;
    }

    public object? GetShardingEntity()
    {
        return table;
    }

    public Expression? GetPredicate()
    {
        return predicate;
    }

    public bool UseQueryable()
    {
        return queryable != null;
    }

    public bool UseValue()
    {
        return keyValue != null;
    }

    public bool UseEntity()
    {
        return table != null;
    }

    public bool UsePredicate()
    {
        return predicate != null;
    }
}

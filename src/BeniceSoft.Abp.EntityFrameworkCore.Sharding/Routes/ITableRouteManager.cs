using System.Collections.Concurrent;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface ITableRouteManager
{
    /// <summary>
    /// 实体对象是否存在分表路由
    /// </summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    bool HasRoute(Type entityType);

    /// <summary>
    /// 直接路由采用默认数据源
    /// </summary>
    /// <param name="entityType"></param>
    /// <param name="routeRoute"></param>
    /// <returns></returns>
    IReadOnlyList<TableRouteUnit> RouteTo(Type entityType, ShardingTableRoute routeRoute);

    /// <summary>
    /// 直接路由
    /// </summary>
    /// <param name="dataSource"></param>
    /// <param name="entityType"></param>
    /// <param name="routeRoute"></param>
    /// <returns></returns>
    IReadOnlyList<TableRouteUnit> RouteTo(string dataSource, Type entityType, ShardingTableRoute routeRoute);

    /// <summary>
    /// 直接路由
    /// </summary>
    /// <param name="routeResult"></param>
    /// <param name="entityType"></param>
    /// <param name="routeRoute"></param>
    /// <returns></returns>
    IReadOnlyList<TableRouteUnit> RouteTo(DataSourceRouteResult routeResult, Type entityType, ShardingTableRoute routeRoute);

    /// <summary>
    /// 获取实体对象的分表路由,如果没有将抛出异常
    /// </summary>
    /// <returns></returns>
    ITableRoute GetRoute(Type entityType);

    /// <summary>
    /// 获取所有的分表路由
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<ITableRoute> GetRoutes();

    /// <summary>
    /// 添加分表路由
    /// </summary>
    /// <param name="route"></param>
    /// <returns></returns>
    /// <exception cref="ShardingInvalidOperationException">对象未配置分库</exception>
    bool AddRoute(ITableRoute route);
}

internal sealed class TableRouteManager(IVirtualDataSource virtualDataSource) : ITableRouteManager
{
    private readonly ConcurrentDictionary<Type, ITableRoute> _tableRoutes = new();

    public bool HasRoute(Type entityType)
    {
        return _tableRoutes.ContainsKey(entityType);
    }

    public ITableRoute GetRoute(Type entityType)
    {
        if (!_tableRoutes.TryGetValue(entityType, out var tableRoute))
        {
            throw new ShardingInvalidOperationException($"entity type :[{entityType.FullName}] not found table route");
        }

        return tableRoute;
    }

    public IReadOnlyList<ITableRoute> GetRoutes()
    {
        return [.. _tableRoutes.Values];
    }

    public bool AddRoute(ITableRoute route)
    {
        if (!route.EntityMetadata.ShardingTable)
        {
            throw new ShardingInvalidOperationException($"{route.EntityMetadata.EntityType.FullName} should configure sharding table");
        }

        return _tableRoutes.TryAdd(route.EntityMetadata.EntityType, route);
    }

    public IReadOnlyList<TableRouteUnit> RouteTo(Type entityType, ShardingTableRoute routeRoute)
    {
        return RouteTo(virtualDataSource.DefaultDataSource, entityType, routeRoute);
    }

    public IReadOnlyList<TableRouteUnit> RouteTo(string dataSource, Type entityType, ShardingTableRoute routeRoute)
    {
        var dataSourceResult = new DataSourceRouteResult(dataSource);
        return RouteTo(dataSourceResult, entityType, routeRoute);
    }

    public IReadOnlyList<TableRouteUnit> RouteTo(DataSourceRouteResult dataSource, Type entityType, ShardingTableRoute routeRoute)
    {
        var route = GetRoute(entityType);
        if (routeRoute.UseQueryable())
        {
            return route.GetRouteList(dataSource, routeRoute.GetQueryable()!, true);
        }

        if (routeRoute.UsePredicate())
        {
            var query = (IShardingEmptyQuery)(Activator.CreateInstance(typeof(ShardingEmptyQuery<>).MakeGenericType(entityType), routeRoute.GetPredicate())
                ?? throw new ShardingException($"Unable to create empty query for type [{entityType}]."));

            return route.GetRouteList(dataSource, query.GetQueryable(), false);
        }

        object? value = null;
        if (routeRoute.UseValue())
        {
            value = routeRoute.GetShardingKeyValue();
        }

        if (routeRoute.UseEntity())
        {
            value = routeRoute.GetShardingEntity()!.GetPropertyValue(route.EntityMetadata.TableProperty!.Name);
        }

        if (value == null)
        {
            throw new ShardingException(" route entity queryable or sharding key value is null ");
        }

        var shardingRouteUnit = route.GetRouteValue(dataSource, value);
        return [shardingRouteUnit];
    }
}

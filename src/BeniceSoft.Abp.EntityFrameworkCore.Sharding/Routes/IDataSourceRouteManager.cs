using System.Collections.Concurrent;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IDataSourceRouteManager
{
    /// <summary>
    /// 实体对象是否存在分库路由
    /// </summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    bool HasRoute(Type entityType);

    /// <summary>
    /// 路由到具体的物理数据源
    /// </summary>
    /// <param name="entityType"></param>
    /// <param name="routeRoute"></param>
    /// <returns>data source names</returns>
    IReadOnlyList<string> RouteTo(Type entityType, ShardingDataSourceRoute routeRoute);

    /// <summary>
    /// 获取当前数据源的路由
    /// </summary>
    /// <returns></returns>
    IDataSourceRoute GetRoute(Type entityType);

    /// <summary>
    /// 添加分库路由
    /// </summary>
    /// <param name="route"></param>
    /// <returns></returns>
    /// <exception cref="ShardingInvalidOperationException">对象未配置分库</exception>
    bool AddRoute(IDataSourceRoute route);
}

internal sealed class DataSourceRouteManager(IEntityMetadataManager entityMetadataManager, IVirtualDataSource virtualDataSource) : IDataSourceRouteManager
{
    private readonly ConcurrentDictionary<Type, IDataSourceRoute> _routes = new();

    public bool AddRoute(IDataSourceRoute route)
    {
        if (!route.EntityMetadata.ShardingDataSource)
        {
            throw new ShardingInvalidOperationException($"{route.EntityMetadata.EntityType.FullName} should configure sharding data source");
        }

        return _routes.TryAdd(route.EntityMetadata.EntityType, route);
    }

    public IDataSourceRoute GetRoute(Type entityType)
    {
        if (!_routes.TryGetValue(entityType, out var route))
        {
            throw new ShardingInvalidOperationException($"entity type :[{entityType.FullName}] not found virtual data source route");
        }

        return route;
    }

    public bool HasRoute(Type entityType)
    {
        return _routes.ContainsKey(entityType);
    }

    public IReadOnlyList<string> RouteTo(Type entityType, ShardingDataSourceRoute routeRoute)
    {
        if (!entityMetadataManager.IsShardingDataSource(entityType))
        {
            return [virtualDataSource.DefaultDataSource];
        }

        var virtualDataSourceRoute = GetRoute(entityType);

        if (routeRoute.UseQueryable())
        {
            return virtualDataSourceRoute.GetRouteList(routeRoute.GetQueryable()!, true);
        }

        if (routeRoute.UsePredicate())
        {
            var query = (IShardingEmptyQuery)(Activator.CreateInstance(typeof(ShardingEmptyQuery<>).MakeGenericType(entityType), routeRoute.GetPredicate())
                ?? throw new ShardingException($"Unable to create empty query for type [{entityType}]."));

            return virtualDataSourceRoute.GetRouteList(query.GetQueryable(), false);
        }

        object? shardingKeyValue = null;
        if (routeRoute.UseValue())
        {
            shardingKeyValue = routeRoute.GetShardingKeyValue();
        }

        if (routeRoute.UseEntity())
        {
            shardingKeyValue = routeRoute.GetShardingDataSource()!.GetPropertyValue(virtualDataSourceRoute.EntityMetadata.DataSourceProperty!.Name);
        }

        if (shardingKeyValue != null)
        {
            var dataSourceName = virtualDataSourceRoute.GetRouteValue(shardingKeyValue);
            return [dataSourceName];
        }

        throw new ShardingNotImplementedException(nameof(ShardingDataSourceRoute));
    }
}

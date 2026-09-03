using BeniceSoft.Core;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IDataSourceRouteRule
{
    DataSourceRouteResult Route(DataSourceRouteRuleContext routeRuleContext);
}

internal sealed class DataSourceRouteRule(IVirtualDataSource virtualDataSource, IEntityMetadataManager entityMetadataManager, IDataSourceRouteManager dataSourceRouteManager) : IDataSourceRouteRule
{
    public DataSourceRouteResult Route(DataSourceRouteRuleContext routeRuleContext)
    {
        var maps = new Dictionary<Type, ISet<string>>();

        foreach (var entity in routeRuleContext.Entities)
        {
            var queryEntity = entity.Key;
            if (!entityMetadataManager.IsShardingDataSource(queryEntity))
            {
                maps.Add(queryEntity, new HashSet<string>() { virtualDataSource.DefaultDataSource });
                continue;
            }

            var configs = dataSourceRouteManager.RouteTo(queryEntity, new ShardingDataSourceRoute(entity.Value ?? routeRuleContext.Queryable));
            if (!maps.TryGetValue(queryEntity, out var value))
            {
                maps.Add(queryEntity, configs.ToHashSet());
            }
            else
            {
                foreach (var dataSource in configs)
                {
                    value.Add(dataSource);
                }
            }
        }

        if (maps.IsNull())
        {
            throw new ShardingException($"data source route not match: {routeRuleContext.Queryable.Expression.Print()}");
        }

        if (maps.Count == 1)
        {
            return new DataSourceRouteResult(maps.First().Value);
        }

        var intersect = maps.Select(o => o.Value).Aggregate((p, n) => p.Intersect(n).ToHashSet());
        return new DataSourceRouteResult(intersect);
    }
}

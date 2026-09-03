using BeniceSoft.Core;
using BeniceSoft.Core.Reflector;
namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface ITableRouteRule
{
    ShardingRouteResult Route(TableRouteRuleContext context);
}

internal sealed class TableRouteRule(ITableRouteManager tableRouteManager,
    IEntityMetadataManager entityMetadataManager, IParallelTableManager parallelTableManager) : ITableRouteRule
{
    private IReadOnlyList<TableRouteUnit> GetEntityRouteUnit(DataSourceRouteResult dataSourceRouteResult, Type shardingEntity, IQueryable queryable)
    {
        var virtualTableRoute = tableRouteManager.GetRoute(shardingEntity);
        return virtualTableRoute.GetRouteList(dataSourceRouteResult, queryable, true);
    }

    public ShardingRouteResult Route(TableRouteRuleContext context)
    {
        var routeMaps = new Dictionary<string, Dictionary<Type, ISet<TableRouteUnit>>>();

        var entities = context.Entities;

        var onlyDataSource = entities.All(o => entityMetadataManager.IsShardingOnlyDataSource(o.Key));
        foreach (var entity in entities)
        {
            var shardingEntity = entity.Key;
            if (!entityMetadataManager.IsShardingTable(shardingEntity))
            {
                continue;
            }

            var shardingRouteUnits = GetEntityRouteUnit(context.RouteResult, shardingEntity, entity.Value ?? context.Queryable);

            foreach (var shardingRouteUnit in shardingRouteUnits)
            {
                var name = shardingRouteUnit.DataSource;

                if (!routeMaps.TryGetValue(name, out var value))
                {
                    routeMaps.Add(name, new Dictionary<Type, ISet<TableRouteUnit>>() { { shardingEntity, new HashSet<TableRouteUnit>() { shardingRouteUnit } } });
                }
                else
                {
                    var routeMap = value;
                    if (!routeMap.TryGetValue(shardingEntity, out var subValue))
                    {
                        routeMap.Add(shardingEntity, new HashSet<TableRouteUnit>() { shardingRouteUnit });
                    }
                    else
                    {
                        subValue.Add(shardingRouteUnit);
                    }
                }
            }
        }

        //相同的数据源进行笛卡尔积
        //[[ds0,01,a],[ds0,02,a],[ds1,01,a]],[[ds0,01,b],[ds0,03,b],[ds1,01,b]]
        //=>
        //[ds0,[{01,a},{01,b}]],[ds0,[{01,a},{03,b}]],[ds0,[{02,a},{01,b}]],[ds0,[{02,a},{03,b}]],[ds1,[{01,a},{01,b}]]
        //如果笛卡尔积

        var units = new List<ISqlRouteUnit>(31);
        var dataSourceCount = 0;
        var isCrossTable = false;
        var tails = false;
        foreach (var dataSourceName in context.RouteResult.Intersect)
        {
            if (routeMaps.TryGetValue(dataSourceName, out var value))
            {
                var routeMap = value;
                var routeResults = routeMap.Select(o => o.Value).Cartesian().Select(o => new TableRouteResult(o.ToList())).Where(o => !o.IsEmpty).ToArray();

                //平行表
                var tableRouteResults = GetTableRouteResults(context, routeResults);
                if (tableRouteResults.IsNotNull())
                {
                    dataSourceCount++;
                    if (tableRouteResults.Length > 1)
                    {
                        isCrossTable = true;
                    }

                    foreach (var tableRouteResult in tableRouteResults)
                    {
                        if (tableRouteResult.ReplaceTables.Count > 1)
                        {
                            isCrossTable = true;
                            if (tableRouteResult.HasDifferentTail)
                            {
                                tails = true;
                            }
                        }

                        units.Add(new SqlRouteUnit(dataSourceName, tableRouteResult));
                    }
                }
            }
            else if (onlyDataSource)
            {
                var tableRouteResult = new TableRouteResult(entities.Keys.Select(o => new TableRouteUnit(dataSourceName, string.Empty, o)).ToList());
                units.Add(new SqlRouteUnit(dataSourceName, tableRouteResult));
            }
        }

        return new ShardingRouteResult(units, units.Count == 0, dataSourceCount > 1, isCrossTable,
            tails);
    }

    private TableRouteResult[] GetTableRouteResults(TableRouteRuleContext tableRouteRuleContext, TableRouteResult[] routeResults)
    {
        if (tableRouteRuleContext.Entities.Count > 1 && routeResults.Length > 0)
        {
            var tables = tableRouteRuleContext.Entities.Keys.Where(entityMetadataManager.IsShardingTable).ToArray();

            if (tables.Length > 1 && parallelTableManager.IsQuery(tables))
            {
                return routeResults.FindAll(o => !o.HasDifferentTail);
            }
        }

        return routeResults;
    }
}

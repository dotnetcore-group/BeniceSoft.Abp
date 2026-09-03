namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface ITableRouteRuleFactory
{
    ShardingRouteResult Route(DataSourceRouteResult routeResult, IQueryable queryable, Dictionary<Type, IQueryable?> entities);
}

internal sealed class TableRouteRuleFactory(ITableRouteRule tableRouteRule) : ITableRouteRuleFactory
{
    private static TableRouteRuleContext CreateContext(DataSourceRouteResult routeResult, IQueryable queryable, Dictionary<Type, IQueryable?> entities)
    {
        return new TableRouteRuleContext(routeResult, queryable, entities);
    }

    public ShardingRouteResult Route(DataSourceRouteResult routeResult, IQueryable queryable, Dictionary<Type, IQueryable?> entities)
    {
        var ruleContext = CreateContext(routeResult, queryable, entities);
        return Route(ruleContext);
    }

    private ShardingRouteResult Route(TableRouteRuleContext ruleContext)
    {
        return tableRouteRule.Route(ruleContext);
    }
}

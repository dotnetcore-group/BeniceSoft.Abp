namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IDataSourceRouteRuleFactory
{
    DataSourceRouteResult Route(IQueryable queryable, IShardingDbContext shardingDbContext, Dictionary<Type, IQueryable?> entities);
}

internal sealed class DataSourceRouteRuleFactory(IDataSourceRouteRule rule) : IDataSourceRouteRuleFactory
{
    public DataSourceRouteResult Route(IQueryable queryable, IShardingDbContext shardingDbContext, Dictionary<Type, IQueryable?> entities)
    {
        var context = new DataSourceRouteRuleContext(queryable, shardingDbContext, entities);
        return rule.Route(context);
    }
}

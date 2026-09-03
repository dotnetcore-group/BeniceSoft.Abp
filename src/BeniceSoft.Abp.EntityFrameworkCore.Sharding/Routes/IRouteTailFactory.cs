using BeniceSoft.Core;
namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IRouteTailFactory
{
    IRouteTail Create(string tail, bool cache = true);

    IRouteTail Create(TableRouteResult tableRouteResult, bool cache = true);
}

public class RouteTailFactory(IEntityMetadataManager entityMetadataManager) : IRouteTailFactory
{
    public IRouteTail Create(string tail)
    {
        return Create(tail, true);
    }

    public IRouteTail Create(string tail, bool cache)
    {
        if (cache)
        {
            return new SingleRouteTail(tail);
        }
        else
        {
            return new NoCacheSingleRouteTail(tail);
        }
    }

    public IRouteTail Create(TableRouteResult tableRouteResult)
    {
        return Create(tableRouteResult, true);
    }

    public IRouteTail Create(TableRouteResult tableRouteResult, bool cache)
    {
        if (tableRouteResult == null || tableRouteResult.ReplaceTables.IsNull())
        {
            if (cache)
            {
                return new SingleRouteTail(string.Empty);
            }
            else
            {
                return new NoCacheSingleRouteTail(string.Empty);
            }
        }

        if (tableRouteResult.ReplaceTables.Count == 1)
        {
            if (cache)
            {
                return new SingleRouteTail(tableRouteResult);
            }
            else
            {
                return new NoCacheSingleRouteTail(tableRouteResult);
            }
        }

        var query = tableRouteResult.ReplaceTables.Select(o => o.EntityType).Any(entityMetadataManager.IsShardingTable);

        return new MultipleRouteTail(tableRouteResult, query);
    }
}

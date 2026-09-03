using BeniceSoft.Core.Strategy;
using Microsoft.Extensions.Logging;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IShardingSeed
{
    void Initialize();
}

internal sealed class ShardingSeed(IShardingProvider shardingProvider, IDbContextAware aware, IShardingRouteOptions routeOptions, IEntityMetadataManager entityMetadataManager, IParallelTableManager parallelTableManager, ILogger<ShardingBootstrapper> logger) : IShardingSeed
{
    private readonly OnceLock _lock = new();

    /// <summary>
    /// 初始化
    /// </summary>
    public void Initialize()
    {
        if (!_lock.IsAcquired)
        {
            return;
        }

        logger.LogDebug("sharding starting......");
        logger.LogDebug("sharding initialize entity metadata......");
        InitializeEntityMetadata();
        logger.LogDebug("sharding initialize parallel table......");
        InitializeParallelTables();
        logger.LogDebug($"sharding complete initialize");
        logger.LogDebug("sharding running......");
    }

    private void InitializeEntityMetadata()
    {
        var shardingEntities = routeOptions.GetTableRoutes().Concat(routeOptions.GetDataSourceRoutes()).ToHashSet();

        foreach (var entityType in shardingEntities)
        {
            var seedType =
                typeof(EntityMetadataSeed<>).MakeGenericType(entityType);

            var seed = (IEntityMetadataSeed)shardingProvider.CreateInstance(seedType);
            seed.Initialize();
        }

        var entities = entityMetadataManager.GetShardingEntities();
        //判断如果有分表的对象那么dbcontext必须继承IShardingTableDbContext
        if (entities.Any(entityMetadataManager.IsShardingTable))
        {
            var dbType = aware.DbType;
            if (!dbType.IsShardingTableDbContext())
            {
                throw new ShardingInvalidOperationException($"db context {dbType},has sharding table route,should be implement {nameof(IShardingTableDbContext)}");
            }
        }
    }

    private void InitializeParallelTables()
    {
        foreach (var node in routeOptions.GetParallelTables())
        {
            var parallelTableComparerType = node.Entities.FirstOrDefault(o => !entityMetadataManager.IsShardingTable(o.Type));
            if (parallelTableComparerType != null)
            {
                throw new ShardingInvalidOperationException(
                    $"{parallelTableComparerType.Type.Name} must is sharding table type");
            }

            parallelTableManager.Add(node);
        }
    }
}

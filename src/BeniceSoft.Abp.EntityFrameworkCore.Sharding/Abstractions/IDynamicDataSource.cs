using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IDynamicDataSource
{
    /// <summary>
    /// 动态初始化数据源仅创建
    /// </summary>
    /// <param name="name"></param>
    /// <param name="createDatabase"></param>
    /// <param name="createTable"></param>
    void Initialize(string name, bool createDatabase, bool createTable);
}

internal sealed class DynamicDataSource(
    IShardingProvider shardingProvider,
    IDbContextCreator dbContextCreator,
    ShardingOptions shardingConfigOptions,
    IVirtualDataSource virtualDataSource,
    IRouteTailFactory routeTailFactory,
    IDataSourceRouteManager dataSourceRouteManager,
    ITableRouteManager tableRouteManager,
    IEntityMetadataManager entityMetadataManager,
    ITableCreator tableCreator,
    ITableEnsureManager tableEnsureManager,
    ILogger<DynamicDataSource> logger) : IDynamicDataSource
{
    public void Initialize(string name, bool createDatabase, bool createTable)
    {
        using var scope = shardingProvider.CreateScope();
        using var shell = dbContextCreator.GetShell(scope);
        var isDefault = virtualDataSource.IsDefault(name);
        var entitiesOnDataSource = GetEntitiesOnDataSource(name).ToList();

        // AutoCreateDataSource：启动时是否建库（null/true 建，false 跳过）
        if (createDatabase && ShouldAutoCreateDataSource(entitiesOnDataSource))
        {
            EnsureCreated(isDefault, shell, name);
        }

        if (createTable)
        {
            var existTables = tableEnsureManager.GetTables((IShardingDbContext)shell, name);
            foreach (var entityMetadata in entitiesOnDataSource)
            {
                // AutoCreateTable：启动时是否建表（null/true 建，false 跳过）
                if (!ShouldAutoCreateTable(entityMetadata))
                {
                    continue;
                }

                CreateDataTable(name, entityMetadata, existTables);
            }
        }
    }

    /// <summary>
    /// 当前数据源上会参与补偿的分片实体（对齐 ShardingCore DefaultDataSourceInitializer 筛选逻辑）。
    /// </summary>
    private IEnumerable<EntityMetadata> GetEntitiesOnDataSource(string dataSource)
    {
        foreach (var entityType in entityMetadataManager.GetShardingEntities())
        {
            var entityMetadata = entityMetadataManager.TryGet(entityType);
            if (entityMetadata == null)
            {
                continue;
            }

            if (entityMetadata.ShardingDataSource)
            {
                var route = dataSourceRouteManager.GetRoute(entityType);
                if (route.GetAll().Contains(dataSource))
                {
                    yield return entityMetadata;
                }
            }
            else if (virtualDataSource.IsDefault(dataSource))
            {
                yield return entityMetadata;
            }
        }
    }

    /// <summary>
    /// null/true → 允许建库；仅分库实体的 <see cref="EntityMetadata.AutoCreateDataSource"/> 参与判断。
    /// 无分库实体或均为 null/true 时 EnsureCreated；该数据源上全部分库实体均为 false 时跳过。
    /// </summary>
    private static bool ShouldAutoCreateDataSource(IReadOnlyList<EntityMetadata> entitiesOnDataSource)
    {
        var dataSourceEntities = entitiesOnDataSource.Where(static m => m.ShardingDataSource).ToList();
        if (dataSourceEntities.Count == 0)
        {
            return true;
        }

        return dataSourceEntities.Any(static m => m.AutoCreateDataSource != false);
    }

    /// <summary>null/true → 允许建表；false → 跳过。</summary>
    private static bool ShouldAutoCreateTable(EntityMetadata entityMetadata)
        => entityMetadata.AutoCreateTable != false;

    private void EnsureCreated(bool isDefault, DbContext context, string dataSource)
    {
        if (context is IShardingDbContext shardingDbContext)
        {
            using var ctx = shardingDbContext.GetWriteDbContext(dataSource, routeTailFactory.Create(string.Empty, false));
            if (isDefault)
            {
                ctx.RemoveShardingTable();
            }
            else
            {
                ctx.RemoveShardingOnlyDataSource();
            }

            ctx.Database.EnsureCreated();
        }
        else
        {
            throw new ShardingInvalidOperationException($"{nameof(IDbContextCreator)}.{nameof(IDbContextCreator.GetShell)} db context type not impl {nameof(IShardingDbContext)}");
        }
    }

    private void CreateDataTable(string dataSource, EntityMetadata entityMetadata, ISet<string> existTables)
    {
        if (!entityMetadata.ShardingTable)
        {
            var physicTableName = $"{entityMetadata.LogicTableName}";
            try
            {
                //添加物理表
                if (!existTables.Contains(physicTableName))
                {
                    tableCreator.Create(dataSource, entityMetadata.EntityType, string.Empty);
                }
            }
            catch (Exception ex)
            {
                HandleCreateTableError(ex, physicTableName);
            }
        }
        else
        {
            var tableRoute = tableRouteManager.GetRoute(entityMetadata.EntityType);
            foreach (var tail in tableRoute.GetTails())
            {
                var physicTableName = $"{entityMetadata.LogicTableName}{entityMetadata.TableSeparator}{tail}";
                try
                {
                    //添加物理表
                    if (!existTables.Contains(physicTableName))
                    {
                        tableCreator.Create(dataSource, entityMetadata.EntityType, tail);
                    }
                }
                catch (Exception ex)
                {
                    HandleCreateTableError(ex, physicTableName);
                }
            }
        }
    }

    private void HandleCreateTableError(Exception ex, string physicTableName)
    {
        // TableCreator 在 !IgnoreCreateTableError 时已记录并抛出 ShardingException
        if (ex is ShardingException)
        {
            throw ex;
        }

        if (!shardingConfigOptions.IgnoreCreateTableError)
        {
            logger.LogWarning(ex, "create table error. Table:{Table}", physicTableName);
            throw new ShardingException($"create table error :{ex.Message}", ex);
        }

        logger.LogWarning(ex, "Table:{Table} maybe created.", physicTableName);
    }
}

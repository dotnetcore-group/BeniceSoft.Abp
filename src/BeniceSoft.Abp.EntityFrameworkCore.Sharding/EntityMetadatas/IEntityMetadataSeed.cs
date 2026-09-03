namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 元数据对象初始化器
/// </summary>
public interface IEntityMetadataSeed
{
    void Initialize();
}

/// <summary>
/// 对象元数据初始化器
/// </summary>
/// <typeparam name="T"></typeparam>
internal sealed class EntityMetadataSeed<T>(IShardingProvider shardingProvider, IShardingRouteOptions routeOptions, IDataSourceRouteManager dataSourceRouteManager, ITableRouteManager tableRouteManager, IEntityMetadataManager entityMetadataManager) : IEntityMetadataSeed
    where T : class
{
    private readonly Type _type = typeof(T);

    /// <summary>
    /// 初始化
    /// 针对对象在dbcontext中的主键获取
    /// 并且针对分库下的特性加接口的支持，然后是分库路由的配置覆盖
    /// 分表下的特性加接口的支持，然后是分表下的路由的配置覆盖
    /// </summary>
    /// <exception cref="ShardingInvalidOperationException"></exception>
    public void Initialize()
    {
        var entityMetadata = new EntityMetadata(_type);
        if (!entityMetadataManager.Add(entityMetadata))
        {
            throw new ShardingInvalidOperationException($"repeat add entity metadata {_type.FullName}");
        }
        //设置标签
        if (routeOptions.TryGetDataSourceRoute<T>(out var dataSourceRouteType))
        {
            var metadataBuilder = new EntityMetadataDataSourceBuilder<T>(entityMetadata);
            //配置属性分库信息
            var dataSourceRoute = CreateDataSourceRoute(dataSourceRouteType);
            if (dataSourceRoute is IEntityMetadataBinder metadataBinder)
            {
                metadataBinder.Initialize(entityMetadata, shardingProvider);
            }
            //配置分库信息
            if (dataSourceRoute is IEntityMetadataDataSource<T> metadataDataSource)
            {
                metadataDataSource.Configure(metadataBuilder);
            }

            dataSourceRouteManager.AddRoute(dataSourceRoute);
            entityMetadata.CheckShardingDataSource();
        }

        if (routeOptions.TryGetTableRoute<T>(out var tableRouteType))
        {
            var metadataBuilder = new EntityMetadataTableBuilder<T>(entityMetadata);
            //配置属性分表信息

            var tableRoute = CreateTableRoute(tableRouteType);
            if (tableRoute is IEntityMetadataBinder metadataBinder)
            {
                metadataBinder.Initialize(entityMetadata, shardingProvider);
            }

            //配置分表信息
            if (tableRoute is IEntityMetadataTable<T> metadataTable)
            {
                metadataTable.Configure(metadataBuilder);
            }
            //创建虚拟表
            tableRouteManager.AddRoute(tableRoute);
            //检测校验分表分库对象元数据
            entityMetadata.CheckShardingTable();
            //添加任务
            if (tableRoute is IShardingJob routeJob && routeJob.Appended)
            {
                var jobEntry = ShardingJobEntryFactory.Create(routeJob);
                shardingProvider.GetRequiredService<IShardingJobManager>().AddJob(jobEntry);
            }
        }

        entityMetadata.CheckMetadata();
    }

    private IDataSourceRoute<T> CreateDataSourceRoute(Type virtualRouteType)
    {
        var instance = shardingProvider.CreateInstance(virtualRouteType);
        return (IDataSourceRoute<T>)instance;
    }

    private ITableRoute<T> CreateTableRoute(Type virtualRouteType)
    {
        var instance = shardingProvider.CreateInstance(virtualRouteType);
        return (ITableRoute<T>)instance;
    }
}

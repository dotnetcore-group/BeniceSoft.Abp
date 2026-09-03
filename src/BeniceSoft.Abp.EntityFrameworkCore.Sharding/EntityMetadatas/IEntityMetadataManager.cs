using BeniceSoft.Core;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Concurrent;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IEntityMetadataManager
{
    /// <summary>
    /// 添加元数据
    /// </summary>
    /// <param name="metadata"></param>
    bool Add(EntityMetadata metadata);

    /// <summary>
    /// 是否为分片对象（分库或分表）
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    bool IsSharding(Type type);

    /// <summary>
    /// 是否分库
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    bool IsShardingDataSource(Type type);

    bool IsShardingOnlyDataSource(Type type);

    /// <summary>
    /// 是否分表
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    bool IsShardingTable(Type type);

    bool IsShardingOnlyTable(Type type);

    EntityMetadata? TryGet(Type type);

    IReadOnlyList<EntityMetadata>? TryGet(string logicName);

    IReadOnlyList<Type> GetShardingEntities();

    bool Initialize(IEntityType entityType);
}

public class EntityMetadataManager(ShardingOptions options) : IEntityMetadataManager
{
    private readonly ConcurrentDictionary<Type, EntityMetadata> _caches = new();
    private readonly ConcurrentDictionary<string, List<EntityMetadata>> _logicTableCaches = new();

    public bool Add(EntityMetadata metadata)
    {
        return _caches.TryAdd(metadata.EntityType, metadata);
    }

    /// <summary>
    /// 对象是否是分表对象
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public bool IsShardingTable(Type type)
    {
        if (!_caches.TryGetValue(type, out var entityMetadata))
        {
            return false;
        }

        return entityMetadata.ShardingTable;
    }

    public bool IsShardingOnlyTable(Type type)
    {
        if (!_caches.TryGetValue(type, out var entityMetadata))
        {
            return false;
        }

        return entityMetadata.ShardingTable && !entityMetadata.ShardingDataSource;
    }

    /// <summary>
    /// 对象是否是分库对象
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public bool IsShardingDataSource(Type type)
    {
        if (!_caches.TryGetValue(type, out var entityMetadata))
        {
            return false;
        }

        return entityMetadata.ShardingDataSource;
    }

    public bool IsShardingOnlyDataSource(Type type)
    {
        if (!_caches.TryGetValue(type, out var entityMetadata))
        {
            return false;
        }

        return entityMetadata.ShardingDataSource && !entityMetadata.ShardingTable;
    }

    /// <summary>
    /// 对象获取没有返回null
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public EntityMetadata? TryGet(Type type)
    {
        if (!_caches.TryGetValue(type, out var entityMetadata))
        {
            return null;
        }

        return entityMetadata;
    }

    public IReadOnlyList<EntityMetadata>? TryGet(string type)
    {
        if (_logicTableCaches.TryGetValue(type, out var metadata))
        {
            return metadata;
        }

        return null;
    }

    /// <summary>
    /// 是否是分片对象(包括分表或者分库)
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public bool IsSharding(Type type)
    {
        if (!_caches.TryGetValue(type, out var metadata))
        {
            return false;
        }

        return metadata.ShardingTable || metadata.ShardingDataSource;
    }

    public IReadOnlyList<Type> GetShardingEntities()
    {
        return _caches.Where(o => o.Value.ShardingTable || o.Value.ShardingDataSource).Select(o => o.Key).ToList();
    }

    public bool Initialize(IEntityType entityType)
    {
        if (_caches.TryGetValue(entityType.ClrType, out var metadata))
        {
            if (options.Doubt)
            {
                if (metadata.ShardingDataSource)
                {
                    foreach (var metadataProperty in metadata.DataSourceProperties)
                    {
                        var propertyName = metadataProperty.Key;
                        var property = entityType.GetProperty(propertyName);
                        if (property.ValueGenerated != ValueGenerated.Never)
                        {
                            throw new ShardingException(
                                $"sharding data source key:{propertyName} is not {nameof(ValueGenerated)}.{nameof(ValueGenerated.Never)}");
                        }
                    }
                }

                if (metadata.ShardingTable)
                {
                    foreach (var metadataProperty in metadata.TableProperties)
                    {
                        var propertyName = metadataProperty.Key;
                        var property = entityType.GetProperty(propertyName);
                        if (property.ValueGenerated != ValueGenerated.Never)
                        {
                            throw new ShardingException($"sharding table key:{propertyName} is not {nameof(ValueGenerated)}.{nameof(ValueGenerated.Never)}");
                        }
                    }
                }
            }

            metadata.SetEntityType(entityType);

            if (metadata.LogicTableName.IsNull())
            {
                throw new ShardingInvalidOperationException($"init model error, cant get logic table name:[{metadata.LogicTableName}] from  entity:[{entityType.ClrType}],is view:[{metadata.IsView}]");
            }

            if (!_logicTableCaches.TryGetValue(metadata.LogicTableName, out var metadatas))
            {
                metadatas = [];
                _logicTableCaches.TryAdd(metadata.LogicTableName, metadatas);
            }

            if (metadatas.All(o => o.EntityType != entityType.ClrType))
            {
                metadatas.Add(metadata);
                return true;
            }

            //添加完成后检查逻辑表对应的对象不可以存在两个以上的分片
            if (metadatas.Count > 1 && metadatas.Exists(o => o.ShardingTable || o.ShardingDataSource))
            {
                throw new ShardingInvalidOperationException($"cant add logic table name caches for metadata:[{metadata.LogicTableName}-{entityType.ClrType}]");
            }
        }

        return false;
    }
}

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IEntityMetadataBinder
{
    IShardingProvider ShardingProvider { get; }

    void Initialize(EntityMetadata entityMetadata, IShardingProvider shardingProvider);
}

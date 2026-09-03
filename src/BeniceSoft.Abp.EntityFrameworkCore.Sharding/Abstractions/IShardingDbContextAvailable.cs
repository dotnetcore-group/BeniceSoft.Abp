namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IShardingDbContextAvailable
{
    IShardingDbContext DbContext { get; }
}

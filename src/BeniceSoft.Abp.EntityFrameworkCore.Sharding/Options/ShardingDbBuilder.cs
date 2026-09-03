using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public class ShardingDbBuilder<T>
    where T : DbContext, IShardingDbContext
{
}

using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public class ShardingOptionsBuilder<T>
    where T : DbContext, IShardingDbContext
{

}

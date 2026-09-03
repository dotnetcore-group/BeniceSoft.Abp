using BeniceSoft.Abp.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests.Fixture;

[ConnectionStringName("Default")]
public class ShardingFixtureDbContext : BeniceSoftShardingAbpDbContext<ShardingFixtureDbContext>, IShardingTableDbContext
{
    public DbSet<ShardLedger> Ledgers { get; set; } = null!;
    public DbSet<ShardBucket> Buckets { get; set; } = null!;
    public DbSet<ShardAreaOrder> AreaOrders { get; set; } = null!;

    public IRouteTail RouteTail { get; set; } = null!;

    public ShardingFixtureDbContext(DbContextOptions<ShardingFixtureDbContext> options) : base(options)
    {
    }

    protected override void OnBeniceSoftModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShardLedger>(e =>
        {
            e.ToTable("shard_ledgers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
            e.Property(x => x.BatchTag).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.BatchTag);
            e.HasIndex(x => x.BizMonth);
        });

        modelBuilder.Entity<ShardBucket>(e =>
        {
            e.ToTable("shard_buckets");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(64).IsRequired();
            e.Property(x => x.BatchTag).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.BatchTag);
            e.HasIndex(x => x.BucketKey);
        });

        modelBuilder.Entity<ShardAreaOrder>(e =>
        {
            e.ToTable("shard_area_orders");
            e.HasKey(x => x.Id);
            e.Property(x => x.Area).HasMaxLength(16).IsRequired();
            e.Property(x => x.Title).HasMaxLength(128).IsRequired();
            e.Property(x => x.BatchTag).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.BatchTag);
            e.HasIndex(x => x.Area);
        });
    }
}

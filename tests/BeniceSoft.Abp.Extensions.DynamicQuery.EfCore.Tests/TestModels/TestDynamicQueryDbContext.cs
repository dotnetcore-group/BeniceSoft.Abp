using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.Extensions.DynamicQuery.EfCore.Tests.TestModels;

public class TestDynamicQueryDbContext : DbContext
{
    public TestDynamicQueryDbContext(DbContextOptions<TestDynamicQueryDbContext> options) : base(options)
    {
    }

    public DbSet<TestEntity> Entities => Set<TestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestEntity>(b =>
        {
            b.HasKey(x => x.Id);
            b.OwnsOne(x => x.Nested);
            b.Ignore(x => x.Tags);
        });
    }
}

using BeniceSoft.Abp.Extensions.AuditTrail.Tests.TestEntities;
using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.Extensions.AuditTrail.Tests;

public class TestDbContext : DbContext
{
    public DbSet<TestProduct> Products { get; set; } = null!;
    public DbSet<TestUntrackedEntity> UntrackedEntities { get; set; } = null!;

    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestProduct>(b =>
        {
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<TestUntrackedEntity>(b =>
        {
            b.HasKey(x => x.Id);
        });
    }
}


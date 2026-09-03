using BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests.Entities;
using BeniceSoft.Abp.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests;

[ConnectionStringName("Default")]
public class TestDbContext : BeniceSoftAbpDbContext<TestDbContext>
{
    public DbSet<TestOrder> TestOrders { get; set; } = null!;

    public DbSet<TestAuditedOrder> TestAuditedOrders { get; set; } = null!;

    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    protected override void OnBeniceSoftModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestOrder>(b =>
        {
            b.ToTable("test_orders");
            b.HasKey(x => x.Id);
            b.Property(x => x.OrderNo).HasMaxLength(50);
        });

        modelBuilder.Entity<TestAuditedOrder>(b =>
        {
            b.ToTable("test_audited_orders");
            b.HasKey(x => x.Id);
            b.Property(x => x.OrderNo).HasMaxLength(50);
        });
    }
}
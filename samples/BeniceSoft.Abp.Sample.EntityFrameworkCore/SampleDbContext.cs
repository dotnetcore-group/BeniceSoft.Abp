using BeniceSoft.Abp.EntityFrameworkCore;
using BeniceSoft.Abp.EntityFrameworkCore.Sharding;
using BeniceSoft.Abp.Sample.Domain;
using BeniceSoft.Core.Strategy;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;

namespace BeniceSoft.Abp.Sample.EntityFrameworkCore;

/// <summary>
/// 订单服务壳 DbContext：仅 <see cref="SalesOrder"/> 分表，其余实体普通表。
/// 复制要点：
/// 1. 继承 <see cref="BeniceSoftShardingAbpDbContext{TDbContext}"/>
/// 2. 有分表实体时实现 <see cref="IShardingTableDbContext"/>，
///    并声明 <see cref="IRouteTail"/> 属性（仅声明，业务不要赋值，由分片引擎在创建物理 DbContext 时注入）
/// 3. OnModelCreating 与普通表相同：ToTable 写逻辑表名；分表物理后缀由路由追加
/// </summary>
[ConnectionStringName("Default")]
public class SampleDbContext : BeniceSoftShardingAbpDbContext<SampleDbContext>, IShardingTableDbContext
{
    public virtual DbSet<AMUser> Users { get; set; }

    public virtual DbSet<AMRole> Roles { get; set; }

    public virtual DbSet<AmUserRole> UserRoles { get; set; }

    public virtual DbSet<BulkDemoItem> BulkDemoItems { get; set; }

    /// <summary>普通表：商品主数据（不分片）。</summary>
    public virtual DbSet<Product> Products { get; set; }

    /// <summary>分表：订单（按 OrderTime 月分片）。</summary>
    public virtual DbSet<SalesOrder> SalesOrders { get; set; }

    /// <summary>框架注入；业务代码不要赋值。</summary>
    public IRouteTail RouteTail { get; set; } = null!;

    public SampleDbContext(DbContextOptions<SampleDbContext> options) : base(options)
    {
    }

    protected override void OnBeniceSoftModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AMUser>(entity =>
        {
            entity.ToTable("am_users");
            entity.Property(x => x.Id).ValueGeneratedOnAdd().HasValueGenerator<SnowDateIdGenerator>();

            entity.HasKey(x => x.Id);

            entity.Metadata.FindNavigation(nameof(AMUser.Roles))?.SetPropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(x => x.Roles).WithOne().HasForeignKey(s => s.UserId);
        });

        modelBuilder.Entity<AMRole>(entity =>
        {
            entity.ToTable("am_roles");
            entity.Property(x => x.Id).ValueGeneratedOnAdd().HasValueGenerator<SnowDateIdGenerator>();
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<AmUserRole>(entity =>
        {
            entity.ToTable("am_userroles");
            entity.HasKey(x => new { x.UserId, x.RoleId });

            entity.Property(x => x.UserId).IsRequired().HasComment("用户id");
            entity.Property(x => x.RoleId).IsRequired().HasComment("角色id");
        });

        modelBuilder.Entity<BulkDemoItem>(entity =>
        {
            entity.ToTable("bulk_demo_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.BatchTag).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.BatchTag);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<SalesOrder>(entity =>
        {
            entity.ToTable("sales_orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OrderNo).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ProductCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.BatchTag).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.OrderNo);
            entity.HasIndex(x => x.ProductCode);
            entity.HasIndex(x => x.BatchTag);
            entity.HasIndex(x => x.OrderTime);
        });
    }
}

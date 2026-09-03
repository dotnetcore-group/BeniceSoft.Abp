using BeniceSoft.Abp.Auth.EntityFrameworkCore;
using BeniceSoft.Abp.EntityFrameworkCore.PostgreSql;
using BeniceSoft.Abp.EntityFrameworkCore.Sharding;
using BeniceSoft.Abp.Sample.Domain;
using BeniceSoft.Abp.Sample.EntityFrameworkCore.Sharding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.Sample.EntityFrameworkCore;

/// <summary>
/// Sample 业务 EF 模块：仓储 + Npgsql + Bulk(Hint) + 订单按月分表（只注册 SalesOrder）。
/// <para>
/// 建表规范（固定顺序，不要颠倒）：
/// 1）先 Migration（update-database / 执行 SQL）落到物理表；
/// 2）再启动应用；
/// 3）Host 启动时 Migrate → UseCompensate，仅为已注册路由的分片实体创建分表物理表（如 sales_orders_yyyyMM）。
/// </para>
/// </summary>
[DependsOn(
    typeof(BeniceSoftAbpAuthEntityFrameworkCoreModule),
    typeof(BeniceSoftAbpEntityFrameworkCorePostgreSqlModule),
    typeof(BeniceSoftAbpEntityFrameworkCoreShardingModule),
    typeof(SampleDomainModule)
)]
public class SampleEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        base.ConfigureServices(context);

        var configuration = context.Services.GetConfiguration();
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required for sharding.");

        context.Services.AddAbpDbContext<SampleDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
            // 自定义仓储覆盖默认 IRepository<SalesOrder>，并暴露 ISalesOrderRepository
            options.AddRepository<SalesOrder, SalesOrderRepository>();
        });

        var tenantA = configuration.GetConnectionString("TenantA")!;
        var tenantB = configuration.GetConnectionString("TenantB")!;

        // 注册分库分表
        context.Services.AddSharding<SampleDbContext>()
            .UseOptions((_, options) =>
            {
                options.WithDefaultDataSource("ds0", connectionString);

                // 分库注册多连接字符串
                options.AdditionalDataSourceFactory = _ => new Dictionary<string, string>
                {
                    ["tenant_a"] = tenantA,
                    ["tenant_b"] = tenantB,
                };

                options.WithShardingQuery((connStr, b) =>
                {
                    b.UseNpgsql(connStr);
                    b.ForRowState();
                    b.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
                });
                options.WithShardingTransaction((conn, b) =>
                {
                    b.UseNpgsql(conn);
                    b.ForRowState();
                    b.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
                });
                options.IgnoreCreateTableError = false;
            })
            .UseRouteOptions((_, routes) =>
            {
                routes.AddTableRoute<SalesOrderMonthRoute>();// 分表

                routes.AddDataSourceRoute<SalesOrderTenantDataSourceRoute>();// 分库
            });

        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure<SampleDbContext>(ctx =>
            {
                ctx.UseNpgsql();

                ctx.DbContextOptions.UseShardingAfter<SampleDbContext>(ctx.ServiceProvider, b =>
                {
                    b.ForRowState();
                    b.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
                });

#if DEBUG
                ctx.DbContextOptions.EnableSensitiveDataLogging();
#endif

            });
        });

        context.Services.AddRowPermissionRepositories<SampleDbContext>();
    }
}

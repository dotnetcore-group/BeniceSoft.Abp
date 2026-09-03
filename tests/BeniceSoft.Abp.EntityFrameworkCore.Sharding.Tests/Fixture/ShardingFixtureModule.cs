using BeniceSoft.Abp.EntityFrameworkCore.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests.Fixture;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(BeniceSoftAbpEntityFrameworkCoreShardingModule),
    typeof(BeniceSoftAbpEntityFrameworkCorePostgreSqlModule)
)]
public class ShardingFixtureModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);

        var configuration = BuildConfiguration();
        context.Services.ReplaceConfiguration(configuration);

        var ds0 = configuration.GetConnectionString("Default")
                  ?? throw new InvalidOperationException("ConnectionStrings:Default missing");
        var ds1 = configuration.GetConnectionString("Ds1")
                  ?? throw new InvalidOperationException("ConnectionStrings:Ds1 missing");

        EnsureDatabaseExists(ds0);
        EnsureDatabaseExists(ds1);

        context.Services.AddAbpDbContext<ShardingFixtureDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
        });

        context.Services.AddSharding<ShardingFixtureDbContext>()
            .UseOptions((_, options) =>
            {
                options.WithDefaultDataSource("ds0", ds0);
                options.AdditionalDataSourceFactory = _ => new Dictionary<string, string>
                {
                    ["ds1"] = ds1
                };
                options.WithShardingQuery((cs, b) =>
                {
                    b.UseNpgsql(cs);
                    SuppressManyProvidersWarning(b);
                });
                options.WithShardingTransaction((conn, b) =>
                {
                    b.UseNpgsql(conn);
                    SuppressManyProvidersWarning(b);
                });
                options.IgnoreCreateTableError = true; // 引擎测试库：重复 Compensate。业务服务必须 false，且先 Migration 再 Compensate。
            })
            .UseRouteOptions((_, routes) =>
            {
                routes.AddTableRoute<ShardLedgerMonthRoute>();
                routes.AddTableRoute<ShardBucketModRoute>();
                routes.AddDataSourceRoute<ShardAreaOrderDataSourceRoute>();
            });

        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure<ShardingFixtureDbContext>(ctx =>
            {
                ctx.DbContextOptions.UseNpgsql(ds0);
                ctx.DbContextOptions.ForRowState();
                SuppressManyProvidersWarning(ctx.DbContextOptions);
                ctx.DbContextOptions.UseShardingAfter<ShardingFixtureDbContext>(ctx.ServiceProvider);
            });
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        context.ServiceProvider.UseCompensate();
    }

    private static void SuppressManyProvidersWarning(DbContextOptionsBuilder builder)
    {
        // 分片按 RouteTail 会创建大量物理 DbContext，易触发 EF 内部 SP 数量告警
        builder.ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
    }

    private static void EnsureDatabaseExists(string connectionString)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            return;
        }

        builder.Database = "postgres";
        using var connection = new Npgsql.NpgsqlConnection(builder.ConnectionString);
        connection.Open();
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name";
        check.Parameters.AddWithValue("name", databaseName);
        if (check.ExecuteScalar() is not null)
        {
            return;
        }

        using var create = connection.CreateCommand();
        create.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        create.ExecuteNonQuery();
    }
}

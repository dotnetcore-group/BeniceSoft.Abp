using BeniceSoft.Abp.Auth.Core;
using BeniceSoft.Abp.EntityFrameworkCore;
using BeniceSoft.Abp.EntityFrameworkCore.PostgreSql;
using BeniceSoft.Abp.EntityFrameworkCore.Sharding;
using BeniceSoft.Abp.Sample.Domain;
using BeniceSoft.Abp.Sample.EntityFrameworkCore;
using BeniceSoft.Core;
using BeniceSoft.Core.Strategy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;
using Volo.Abp.Uow;
using Xunit;

namespace BeniceSoft.Abp.Sample.Tests;

/// <summary>
/// 连接 Sample Host 真实 PG 测试库，验证 Bulk / Sequence / Hint / ForceSave。
/// 表结构依赖模块初始化时的 Migration + Compensate，测试内不自建表。
/// </summary>
public class PgBulkIntegrationTests : AbpIntegratedTest<PgBulkIntegrationTestModule>
{
    public const string SequenceName = "bulk_demo_seq";

    private readonly IDbContextProvider<SampleDbContext> _dbContextProvider;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public PgBulkIntegrationTests()
    {
        _dbContextProvider = GetRequiredService<IDbContextProvider<SampleDbContext>>();
        _unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
    }

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    [Fact]
    public async Task Bulk_Insert_Update_Merge_Delete_Should_Work()
    {
        var batchTag = $"test-{Guid.NewGuid():N}"[..20];

        await WithUow(async db =>
        {
            var items = Enumerable.Range(0, 15).Select(i => new BulkDemoItem(
                Guid.NewGuid(),
                $"{batchTag}-{i:D3}",
                $"name-{i}",
                i + 1,
                batchTag)).ToList();

            var inserted = await db.BulkInsertAsync(items);
            inserted.ShouldBe(15);

            var loaded = await db.BulkDemoItems.AsNoTracking().Where(x => x.BatchTag == batchTag).ToListAsync();
            loaded.Count.ShouldBe(15);

            foreach (var item in loaded)
            {
                item.Quantity = 100;
                item.Name = $"{item.Code}-u";
                item.Version++;
            }

            var updated = await db.BulkUpdateAsync(loaded);
            updated.ShouldBe(15);

            var afterUpdate = await db.BulkDemoItems.AsNoTracking().Where(x => x.BatchTag == batchTag).ToListAsync();
            afterUpdate.ShouldAllBe(x => x.Quantity == 100);

            var mergeItems = afterUpdate.Take(5).ToList();
            foreach (var item in mergeItems)
            {
                item.Quantity = 200;
                item.Version++;
            }

            for (var i = 0; i < 3; i++)
            {
                mergeItems.Add(new BulkDemoItem(
                    Guid.NewGuid(),
                    $"{batchTag}-m{i}",
                    $"merge-new-{i}",
                    1,
                    batchTag));
            }

            var merged = await db.BulkMergeAsync(mergeItems);
            merged.ShouldBeGreaterThan(0);

            var all = await db.BulkDemoItems.AsNoTracking().Where(x => x.BatchTag == batchTag).ToListAsync();
            all.Count.ShouldBe(18);

            var deleted = await db.BulkDeleteAsync(all);
            deleted.ShouldBe(18);

            (await db.BulkDemoItems.CountAsync(x => x.BatchTag == batchTag)).ShouldBe(0);
        });
    }

    [Fact]
    public async Task BulkOperation_Should_Share_Transaction()
    {
        var batchTag = $"op-{Guid.NewGuid():N}"[..20];

        await WithUow(async db =>
        {
            var items = Enumerable.Range(0, 8).Select(i => new BulkDemoItem(
                Guid.NewGuid(),
                $"{batchTag}-{i}",
                $"op-{i}",
                i,
                batchTag)).ToList();

            await using var op = db.BulkOperation();
            await op.BulkInsertAsync(items);
            foreach (var item in items)
            {
                item.Quantity = 7;
                item.Version++;
            }

            await op.BulkUpdateAsync(items);
            await op.CommitAsync();

            var loaded = await db.BulkDemoItems.AsNoTracking().Where(x => x.BatchTag == batchTag).ToListAsync();
            loaded.Count.ShouldBe(8);
            loaded.ShouldAllBe(x => x.Quantity == 7);

            await db.BulkDeleteAsync(loaded);
        });
    }

    [Fact]
    public async Task GetSequence_Should_Return_Increasing_Values()
    {
        await WithUow(async db =>
        {
            var values = await db.Database.GetSequenceAsync<long>(SequenceName, 5);
            values.Length.ShouldBe(5);
            for (var i = 1; i < values.Length; i++)
            {
                values[i].ShouldBeGreaterThan(values[i - 1]);
            }
        });
    }

    [Fact]
    public async Task ForUpdate_Query_Should_Execute()
    {
        var batchTag = $"fu-{Guid.NewGuid():N}"[..20];

        await WithUow(async db =>
        {
            var items = new List<BulkDemoItem>
            {
                new(Guid.NewGuid(), $"{batchTag}-1", "a", 1, batchTag),
                new(Guid.NewGuid(), $"{batchTag}-2", "b", 2, batchTag)
            };
            await db.BulkInsertAsync(items);

            var locked = await db.BulkDemoItems.Where(x => x.BatchTag == batchTag).ForUpdate().ToListAsync();
            locked.Count.ShouldBe(2);

            await db.BulkDeleteAsync(locked);
        });
    }

    [Fact]
    public async Task ForceSaveChange_Should_Retry_On_Concurrency_Conflict()
    {
        var batchTag = $"fs-{Guid.NewGuid():N}"[..20];

        await WithUow(async db =>
        {
            var item = new BulkDemoItem(Guid.NewGuid(), $"{batchTag}-1", "force", 1, batchTag);
            await db.BulkInsertAsync([item]);

            var tracked = await db.BulkDemoItems.FirstAsync(x => x.Id == item.Id);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE bulk_demo_items SET \"Version\" = \"Version\" + 1 WHERE \"Id\" = {item.Id}");

            tracked.Name = "force-ok";
            tracked.Quantity = 42;

            var saved = await db.ForceSaveChangeAsync(retryCount: 3);
            saved.ShouldBeGreaterThan(0);

            var reloaded = await db.BulkDemoItems.AsNoTracking().SingleAsync(x => x.Id == item.Id);
            reloaded.Name.ShouldBe("force-ok");
            reloaded.Quantity.ShouldBe(42);

            await db.BulkDeleteAsync([reloaded]);
        });
    }

    private async Task WithUow(Func<SampleDbContext, Task> action)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var db = await _dbContextProvider.GetDbContextAsync();
        await action(db);
        await uow.CompleteAsync();
    }
}

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(SampleEntityFrameworkCoreModule)
)]
public class PgBulkIntegrationTestModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.ReplaceConfiguration(BuildHostConfiguration());
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);

        Singleton<SnowDateIdGenerator>.Instance ??= new SnowDateIdGenerator();
        context.Services.AddSingleton<ICurrentUserPermissionAccessor, EmptyCurrentUserPermissionAccessor>();

        var configuration = context.Services.GetConfiguration();
        var connectionString = configuration.GetConnectionString("Default")
                               ?? throw new InvalidOperationException("ConnectionStrings:Default is missing.");

        EnsureDatabaseExists(connectionString);

        // 二次 Configure 覆盖连接串时必须再用 UseShardingAfter，禁止只写 UseNpgsql（会冲掉分片）。
        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure<SampleDbContext>(ctx =>
            {
                ctx.DbContextOptions.UseNpgsql(connectionString);
                ctx.DbContextOptions.UseShardingAfter<SampleDbContext>(ctx.ServiceProvider, b => b.ForRowState());
            });
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // 建表规范：① Migration 先落物理表 ② 再 UseCompensate 建分表物理表。顺序不可反。
        using (var scope = context.ServiceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            db.Database.Migrate();
        }

        context.ServiceProvider.UseCompensate();
    }

    private sealed class EmptyCurrentUserPermissionAccessor : ICurrentUserPermissionAccessor
    {
        public IUserPermission? UserPermission { get; set; }
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

    /// <summary>
    /// 取 Host appsettings 连接信息，但库名改为 wecharmer_sample_tests（独立测试库，走正规 Migration）。
    /// </summary>
    private static IConfigurationRoot BuildHostConfiguration()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "BeniceSoft.Abp.Sample.Host")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "samples", "BeniceSoft.Abp.Sample.Host")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "samples", "BeniceSoft.Abp.Sample.Host"))
        };

        var hostDir = candidates.FirstOrDefault(Directory.Exists)
                      ?? throw new DirectoryNotFoundException("Cannot locate BeniceSoft.Abp.Sample.Host for connection string.");

        var hostConfig = new ConfigurationBuilder()
            .SetBasePath(hostDir)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var hostCs = hostConfig.GetConnectionString("Default")
                     ?? throw new InvalidOperationException("Host ConnectionStrings:Default missing.");
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(hostCs)
        {
            Database = "wecharmer_sample_tests"
        };

        var map = hostConfig.AsEnumerable()
            .Where(kv => kv.Value is not null)
            .ToDictionary(kv => kv.Key, kv => (string?)kv.Value);
        map["ConnectionStrings:Default"] = builder.ConnectionString;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(map)
            .Build();
    }
}

using BeniceSoft.Abp.Extensions.RabbitMQ.EntityFrameworkCore;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security;
using Volo.Abp.Testing;
using Volo.Abp.Uow;
using Xunit;

namespace BeniceSoft.Abp.Extensions.RabbitMQ.Tests;

/// <summary>
/// Runs only when env BENICESOFT_TEST_PG holds a PostgreSQL connection string. Uses unique table names and drops them.
/// </summary>
public class PostgreSqlOutboxTests : AbpIntegratedTest<PostgreSqlTestModule>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options) => options.UseAutofac();

    protected override void BeforeAddApplication(IServiceCollection services)
    {
        services.ReplaceConfiguration(new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build());
    }

    [Fact]
    public async Task PostgreSql_AutoCreate_Rollback_Commit_And_Dispatch()
    {
        Assert.SkipWhen(PostgreSqlTestModule.ConnectionString.IsNullOrWhiteSpace(), "BENICESOFT_TEST_PG not set");
        var options = GetRequiredService<Microsoft.Extensions.Options.IOptions<RabbitOutboxOptions>>().Value;
        var queue = $"benicesoft.tests.pg.{Guid.NewGuid():N}";
        try
        {
            await using (var channel = await GetRequiredService<IRabbitConnection>().CreateChannelAsync(Ct))
            {
                await RabbitRelational.Work(queue).ConsumerDeclareAsync(channel, Ct);
            }

            var uowManager = GetRequiredService<IUnitOfWorkManager>();
            var outbox = GetRequiredService<IRabbitOutboxPublisher>();

            using (var uow = uowManager.Begin(requiresNew: true, isTransactional: true))
            {
                await outbox.PublishToQueueAsync(queue, new TestWorkMessage { Id = "rollback" }, cancellationToken: Ct);
                await uow.RollbackAsync(Ct);
            }

            (await CountAsync(options.OutboxTableName)).ShouldBe(0); // table auto-created, row rolled back

            string messageId;
            using (var uow = uowManager.Begin(requiresNew: true, isTransactional: true))
            {
                messageId = await outbox.PublishToQueueAsync(queue, new TestWorkMessage { Id = "commit" }, cancellationToken: Ct);
                await uow.CompleteAsync(Ct);
            }

            (await CountAsync(options.OutboxTableName)).ShouldBe(1);
            (await GetRequiredService<RabbitOutboxDispatcher>().DispatchOnceAsync(Ct)).ShouldBe(1);
            (await CountAsync(options.OutboxTableName, "status = 1")).ShouldBe(1);

            await using var check = await GetRequiredService<IRabbitConnection>().CreateChannelAsync(Ct);
            var delivered = await check.BasicGetAsync(queue, true, Ct);
            delivered.ShouldNotBeNull();
            delivered.BasicProperties.MessageId.ShouldBe(messageId);
        }
        finally
        {
            await using (var channel = await GetRequiredService<IRabbitConnection>().CreateChannelAsync())
            {
                await channel.QueueDeleteAsync(queue, false, false);
            }

            await using var connection = new Npgsql.NpgsqlConnection(PostgreSqlTestModule.ConnectionString);
            await connection.ExecuteAsync($"DROP TABLE IF EXISTS {options.OutboxTableName}; DROP TABLE IF EXISTS {options.InboxTableName};");
        }
    }

    private static async Task<long> CountAsync(string table, string? where = null)
    {
        await using var connection = new Npgsql.NpgsqlConnection(PostgreSqlTestModule.ConnectionString);
        return await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {table}" + (where == null ? "" : " WHERE " + where));
    }
}

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpSecurityModule),
    typeof(AbpMultiTenancyModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule),
    typeof(BeniceSoftAbpRabbitMqEntityFrameworkCoreModule)
)]
public class PostgreSqlTestModule : AbpModule
{
    public static string? ConnectionString => Environment.GetEnvironmentVariable("BENICESOFT_TEST_PG");

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        Configure<AbpDbConnectionOptions>(o => o.ConnectionStrings.Default = ConnectionString ?? "Host=localhost");
        context.Services.AddAbpDbContext<PgTestDbContext>();
        Configure<AbpDbContextOptions>(o => o.UseNpgsql());
        Configure<RabbitOutboxOptions>(o =>
        {
            o.DispatcherEnabled = false;
            o.OutboxTableName = "rabbit_outbox_test_" + suffix;
            o.InboxTableName = "rabbit_inbox_test_" + suffix;
        });
    }
}

public class PgTestDbContext : AbpDbContext<PgTestDbContext>
{
    public PgTestDbContext(DbContextOptions<PgTestDbContext> options) : base(options)
    {
    }
}
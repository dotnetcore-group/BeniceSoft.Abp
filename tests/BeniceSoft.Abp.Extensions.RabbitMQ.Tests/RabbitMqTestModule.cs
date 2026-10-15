using BeniceSoft.Abp.Extensions.RabbitMQ.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.Extensions.RabbitMQ.Tests;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpSecurityModule),
    typeof(AbpMultiTenancyModule),
    typeof(BeniceSoftAbpRabbitMqModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(BeniceSoftAbpRabbitMqEntityFrameworkCoreModule)
)]
public class RabbitMqTestModule : AbpModule
{
    private SqliteConnection? _sqliteConnection;

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<ReceivedMessageCollector>();
        context.Services.AddTransient<TestWorkMessageHandler>();
        context.Services.AddSingleton<HandlerCallLog>();
        context.Services.AddTransient<CountingMessageHandler>();

        // A plain business DbContext: no outbox/inbox entities, no migrations. Fresh in-memory DB per test app.
        _sqliteConnection = new SqliteConnection("Data Source=:memory:");
        _sqliteConnection.Open();
        var connection = _sqliteConnection;

        context.Services.AddAbpDbContext<TestBusinessDbContext>();
        Configure<AbpDbContextOptions>(options =>
            options.Configure(c => c.DbContextOptions.UseSqlite(connection)));

        // Tests drive the dispatcher explicitly via DispatchOnceAsync.
        Configure<RabbitOutboxOptions>(o => o.DispatcherEnabled = false);
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // Business tables only (stands in for the host's own migrations).
        using var scope = context.ServiceProvider.CreateScope();
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
        var dbContext = scope.ServiceProvider
            .GetRequiredService<IDbContextProvider<TestBusinessDbContext>>()
            .GetDbContextAsync().GetAwaiter().GetResult();
        dbContext.Database.EnsureCreated();
        uow.CompleteAsync().GetAwaiter().GetResult();
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        _sqliteConnection?.Dispose();
    }
}

public class TestOrder
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class TestBusinessDbContext : AbpDbContext<TestBusinessDbContext>
{
    public TestBusinessDbContext(DbContextOptions<TestBusinessDbContext> options) : base(options)
    {
    }

    public DbSet<TestOrder> Orders => Set<TestOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<TestOrder>(b =>
        {
            b.ToTable("test_orders");
            b.HasKey(x => x.Id);
        });
    }
}
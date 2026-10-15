using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.Extensions.RabbitMQ.EntityFrameworkCore;

/// <summary>
/// Zero-setup outbox / inbox: depend on this module. Tables are created automatically (raw SQL, per provider)
/// on first use and at dispatcher startup. No DbContext changes or migrations needed.
/// </summary>
[DependsOn(
    typeof(BeniceSoftAbpRabbitMqModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class BeniceSoftAbpRabbitMqEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        services.AddOptions<RabbitOutboxOptions>();
        services.TryAddSingleton<RabbitOutboxSignal>();
        services.TryAddSingleton<RabbitMessagingTableInitializer>();
        services.TryAddTransient<RabbitMessagingDbContextResolver>();
        services.TryAddTransient<IRabbitOutboxPublisher, RabbitOutboxPublisher>();
        services.TryAddTransient<IRabbitInboxStore, RabbitInboxStore>();
        services.TryAddSingleton<RabbitOutboxDispatcher>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<RabbitOutboxDispatcher>());
    }

    public override void PostConfigureServices(ServiceConfigurationContext context)
    {
        // Auto-detect the host DbContext when exactly one is registered.
        // AddAbpDbContext<T>() registers DbContextOptions<T>.
        var dbContextTypes = context.Services
            .Select(x => x.ServiceType)
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(DbContextOptions<>))
            .Select(t => t.GetGenericArguments()[0])
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IEfCoreDbContext).IsAssignableFrom(t))
            .Distinct()
            .ToList();
        if (dbContextTypes.Count == 1)
        {
            Configure<RabbitOutboxOptions>(o => o.DbContextType ??= dbContextTypes[0]);
        }
    }
}
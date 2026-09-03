using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EventBus.Distributed;

namespace BeniceSoft.Abp.EventBus.Dtm.EntityFrameworkCore;

public static class EfCoreDtmTransactionsExtensions
{
    public static void UseDbContextWithDtmTransactions(this AbpDistributedEventBusOptions options, Type dbContextType)
    {
        options.Outboxes.Configure(config =>
        {
            config.UseDbContextWithDtmOutbox(dbContextType);
        });

        options.Inboxes.Configure(config =>
        {
            config.UseDbContextWithDtmInbox(dbContextType);
        });
    }

    public static void UseDbContextWithDtmTransactions<TDbContext>(this AbpDistributedEventBusOptions options)
        where TDbContext : class, IEfCoreDbContext
    {
        options.UseDbContextWithDtmTransactions(typeof(TDbContext));
    }

    public static IServiceCollection UseDbContextWithDtmTransactions(this IServiceCollection services, Type dbContextType)
    {
        services.Configure<AbpDistributedEventBusOptions>(options =>
        {
            options.UseDbContextWithDtmTransactions(dbContextType);
        });

        services.Configure<DtmTransactionDbContextOptions>(options =>
        {
            options.DefaultDbContextTypeName = dbContextType.AssemblyQualifiedName;
        });

        return services;
    }

    public static IServiceCollection UseDbContextWithDtmTransactions<TDbContext>(this IServiceCollection services)
        where TDbContext : class, IEfCoreDbContext
    {
        return services.UseDbContextWithDtmTransactions(typeof(TDbContext));
    }
}

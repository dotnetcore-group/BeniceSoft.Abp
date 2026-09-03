using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.EventBus.Dtm;

public static class AbpEventBusDtmExtensions
{
    public static IServiceCollection AddDtmBoxes(this IServiceCollection services, Action<DtmEventBoxesOptions> setupAction)
    {
        services.Configure(setupAction);

        AddDtmOutbox(services);
        AddDtmInbox(services);

        return services;
    }

    private static IServiceCollection AddDtmOutbox(this IServiceCollection services)
    {
        services.TryAddTransient<DtmUnitOfWork>();
        services.TryAddTransient<DtmOutboxSender>();
        services.Replace(ServiceDescriptor.Transient<IUnitOfWork, DtmUnitOfWork>());
        services.Replace(ServiceDescriptor.Transient<IOutboxSender, DtmOutboxSender>());

        return services;
    }

    private static IServiceCollection AddDtmInbox(this IServiceCollection services)
    {
        services.TryAddTransient<DtmInboxProcessor>();
        services.Replace(ServiceDescriptor.Transient<IInboxProcessor, DtmInboxProcessor>());

        return services;
    }
}

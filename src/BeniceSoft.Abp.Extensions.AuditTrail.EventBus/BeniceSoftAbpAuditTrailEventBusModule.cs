using BeniceSoft.Abp.Extensions.AuditTrail.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.EventBus;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.Extensions.AuditTrail.EventBus;

[DependsOn(
    typeof(AbpEventBusModule)
)]
public class BeniceSoftAbpAuditTrailEventBusModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Replace(ServiceDescriptor.Transient<IEntityChangeDispatcher, EventBusEntityChangeDispatcher>());
    }
}


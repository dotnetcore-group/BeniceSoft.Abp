using BeniceSoft.Abp.Core;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Volo.Abp.RabbitMQ;

namespace BeniceSoft.Abp.Extensions.RabbitMQ;

[DependsOn(
    typeof(BeniceSoftAbpCoreModule),
    typeof(AbpRabbitMqModule)
)]
public class BeniceSoftAbpRabbitMqModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddBeniceSoftRabbitMq();
    }
}

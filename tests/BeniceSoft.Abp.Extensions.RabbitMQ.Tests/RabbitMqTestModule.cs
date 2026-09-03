using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security;

namespace BeniceSoft.Abp.Extensions.RabbitMQ.Tests;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpSecurityModule),
    typeof(AbpMultiTenancyModule),
    typeof(BeniceSoftAbpRabbitMqModule)
)]
public class RabbitMqTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<ReceivedMessageCollector>();
        context.Services.AddTransient<TestWorkMessageHandler>();
    }
}

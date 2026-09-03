using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

[DependsOn(
    typeof(BeniceSoftAbpEventBusDtmModule),
    typeof(AbpEventBusRabbitMqModule)
)]
public class BeniceSoftAbpEventBusDtmHttpModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration().GetSection("EventBus");

        context.Services.AddAbpDtmHttp(
            options =>
            {
                options.ActionApiToken = configuration["DTM:ActionApiToken"] ?? options.ActionApiToken;
                options.AppUrl = configuration["DTM:AppUrl"] ?? options.AppUrl;
                options.DtmUrl = configuration["DTM:DtmUrl"] ?? options.DtmUrl;
                options.Timeout = configuration.GetValue<int?>("DTM:Timeout") ?? options.Timeout;
                options.MessageTimeoutToFail = configuration.GetValue<int?>("DTM:MessageTimeoutToFail") ?? options.MessageTimeoutToFail;
                options.MessageRetryInterval = configuration.GetValue<int?>("DTM:MessageRetryInterval") ?? options.MessageRetryInterval;
                options.MessageRetryLimit = configuration.GetValue<int?>("DTM:MessageRetryLimit") ?? options.MessageRetryLimit;
                options.ProcessedGidCacheSeconds = configuration.GetValue<int?>("DTM:ProcessedGidCacheSeconds") ?? options.ProcessedGidCacheSeconds;
            },
            options =>
            {
                options.EnableWaitResult = configuration.GetValue<bool?>("DTM:GlobalTransaction:EnableWaitResult") ?? options.EnableWaitResult;
                options.TimeoutToFail = configuration.GetValue<int?>("DTM:GlobalTransaction:TimeoutToFail") ?? options.TimeoutToFail;
                options.RetryInterval = configuration.GetValue<int?>("DTM:GlobalTransaction:RetryInterval") ?? options.RetryInterval;
                options.RetryLimit = configuration.GetValue<int?>("DTM:GlobalTransaction:RetryLimit") ?? options.RetryLimit;
            });

        context.Services.AddDtmCallbackHandlersFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());
    }
}

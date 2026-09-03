using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace BeniceSoft.Abp.Sample.Application.IntegrationEvents;

public class SampleDtmHttpEventHandler : IDistributedEventHandler<SampleDtmHttpEvent>, ITransientDependency
{
    private readonly ILogger<SampleDtmHttpEventHandler> _logger;

    public SampleDtmHttpEventHandler(ILogger<SampleDtmHttpEventHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleEventAsync(SampleDtmHttpEvent eventData)
    {
        _logger.LogInformation("收到分布式事件: {EventName}, TestId={TestId}", nameof(SampleDtmHttpEvent), eventData.TestId);
        return Task.CompletedTask;
    }
}

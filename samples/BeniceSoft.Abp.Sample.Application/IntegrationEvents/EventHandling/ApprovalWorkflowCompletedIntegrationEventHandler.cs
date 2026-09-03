using BeniceSoft.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace BeniceSoft.Abp.Sample.Application.IntegrationEvents.EventHandling;

public class ApprovalWorkflowCompletedIntegrationEventHandler
    : IDistributedEventHandler<ApprovalWorkflowCompletedIntegrationEvent>, ITransientDependency
{
    private readonly ILogger<ApprovalWorkflowCompletedIntegrationEventHandler> _logger;

    public ApprovalWorkflowCompletedIntegrationEventHandler(ILogger<ApprovalWorkflowCompletedIntegrationEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleEventAsync(ApprovalWorkflowCompletedIntegrationEvent eventData)
    {

        _logger.LogInformation("收到审批流完成事件: {0}", JsonUtils.Serialize(eventData));

    }
}

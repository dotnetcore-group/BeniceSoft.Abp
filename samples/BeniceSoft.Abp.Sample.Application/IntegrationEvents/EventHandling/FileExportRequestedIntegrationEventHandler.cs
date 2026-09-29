using BeniceSoft.Abp.Core.Messaging;
using BeniceSoft.Abp.Core.Users;
using BeniceSoft.Abp.Sample.Application.BackgroundTasks;
using BeniceSoft.Abp.Sample.Application.IntegrationEvents.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.Sample.Application.IntegrationEvents.EventHandling;

public class FileExportRequestedIntegrationEventHandler
    : IDistributedEventHandler<FileExportRequestedIntegrationEvent>, ITransientDependency
{
    private readonly ILogger<FileExportRequestedIntegrationEventHandler> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IBeniceSoftCurrentUser _currentUser;

    public FileExportRequestedIntegrationEventHandler(
        ILogger<FileExportRequestedIntegrationEventHandler> logger,
        IBackgroundJobManager backgroundJobManager,
        IBeniceSoftCurrentUser currentUser)
    {
        _logger = logger;
        _backgroundJobManager = backgroundJobManager;
        _currentUser = currentUser;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(FileExportRequestedIntegrationEvent eventData)
    {
        var userClaims = MessageContextTransfer.EncodeClaims(_currentUser.GetAllClaims());

        var jobId = await _backgroundJobManager.EnqueueAsync(new FileExportHandlingArg
        {
            TaskId = eventData.TaskId,
            TenantId = eventData.TenantId,
            BizType = eventData.BizType,
            Title = eventData.Title,
            ParamsJson = eventData.ParamsJson,
            OperatorUserId = _currentUser.Id,
            UserClaims = userClaims
        });

        _logger.LogWarning(
            "Sample 已入队导出 Job: JobId={JobId} TaskId={TaskId} TenantId={TenantId} BizType={BizType} OperatorUserId={OperatorUserId}",
            jobId,
            eventData.TaskId,
            eventData.TenantId,
            eventData.BizType,
            _currentUser.Id);
    }
}

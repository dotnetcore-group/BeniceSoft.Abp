using BeniceSoft.Abp.Core.Users;
using BeniceSoft.Abp.Sample.Application.IntegrationEvents.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.Sample.Application.IntegrationEvents.EventHandling;

public class FileExportRequestedIntegrationEventHandler
: IDistributedEventHandler<FileExportRequestedIntegrationEvent>, ITransientDependency
{
    private readonly ILogger<FileExportRequestedIntegrationEventHandler> _logger;

    private readonly IBeniceSoftCurrentUser _currentUser;

    public FileExportRequestedIntegrationEventHandler(
        ILogger<FileExportRequestedIntegrationEventHandler> logger,
        IBeniceSoftCurrentUser currentUser)
    {
        _logger = logger;
        _currentUser = currentUser;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(FileExportRequestedIntegrationEvent eventData)
    {
        var userid = _currentUser.Id;

        _logger.LogInformation("WarehouseCenter文件导出任务已创建:");
    }

}

using BeniceSoft.Abp.Core.Messaging;
using BeniceSoft.Abp.Sample.RemoteService.Abstractions;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Http.Client.Proxying;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.Sample.Application.BackgroundTasks;

/// <summary>
/// 对齐 Warehouse FileExportHandlingJob：Change 租户后回调 FileCenter Report。
/// </summary>
public class FileExportHandlingJob : AsyncBackgroundJob<FileExportHandlingArg>, ITransientDependency
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IFileClient _fileClient;
    private readonly IProxyHttpClientFactory _proxyHttpClientFactory;
    private readonly ILogger<FileExportHandlingJob> _logger;

    public FileExportHandlingJob(
        ICurrentTenant currentTenant,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IFileClient fileClient,
        IProxyHttpClientFactory proxyHttpClientFactory,
        ILogger<FileExportHandlingJob> logger)
    {
        _currentTenant = currentTenant;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _fileClient = fileClient;
        _proxyHttpClientFactory = proxyHttpClientFactory;
        _logger = logger;
    }

    [UnitOfWork]
    public override async Task ExecuteAsync(FileExportHandlingArg args)
    {
        var principal = MessageContextTransfer.DecodePrincipal(args.UserClaims, "BackgroundJob");

        using (principal is null ? null : _currentPrincipalAccessor.Change(principal))
        using (_currentTenant.Change(args.TenantId))
        {
            var tenantKey = TenantResolverConsts.DefaultTenantKey;
            var client = _proxyHttpClientFactory.Create("Wecharmer.FileCenter");
            client.DefaultRequestHeaders.TryGetValues(tenantKey, out var headerValues);

            _logger.LogWarning(
                "Sample export job: TaskId={TaskId} ArgTenantId={ArgTenantId} AmbientTenant={AmbientTenant} FactoryType={FactoryType} Outbound__tenant={OutboundTenant}",
                args.TaskId,
                args.TenantId,
                _currentTenant.Id,
                _proxyHttpClientFactory.GetType().FullName,
                headerValues is null ? "<none>" : string.Join(",", headerValues));

            await _fileClient.ReportAsync(
                args.TaskId,
                success: true,
                resultFileId: null,
                message: "sample-job-callback");
        }
    }
}

public class FileExportHandlingArg
{
    public long TaskId { get; set; }

    public Guid? TenantId { get; set; }

    public string BizType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? ParamsJson { get; set; }

    public long? OperatorUserId { get; set; }

    public string? UserClaims { get; set; }
}

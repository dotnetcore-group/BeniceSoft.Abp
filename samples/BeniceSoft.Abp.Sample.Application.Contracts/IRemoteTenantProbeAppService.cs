namespace BeniceSoft.Abp.Sample.Application.Contracts;

/// <summary>
/// 模拟 Warehouse 后台 Job：Change(租户) 后走 ABP 远程 HttpClient，探测 __tenant 是否出站/入站生效。
/// </summary>
public interface IRemoteTenantProbeAppService
{
    /// <summary>
    /// 接收端：返回管道解析后的 CurrentTenant，以及原始请求头 __tenant。
    /// </summary>
    Task<RemoteTenantEchoDto> EchoTenantAsync();

    /// <summary>
    /// 发送端（模拟 Job）：Change(tenantId) 后 Create Proxy HttpClient，查看 Factory 类型与 DefaultRequestHeaders。
    /// </summary>
    Task<RemoteTenantFactoryProbeDto> ProbeFactoryUnderTenantChangeAsync(Guid? tenantId);

    /// <summary>
    /// 完整回路：Change → Factory.Create → 带出站头请求本机 EchoTenant，对比两端租户。
    /// </summary>
    Task<RemoteTenantRoundTripDto> RoundTripUnderTenantChangeAsync(Guid? tenantId, string? echoBaseUrl = null);

    /// <summary>
    /// 带机器 Token + __tenant 调 FileCenter CreateExport，触发集成事件（由 Sample Job 回调 Report）。
    /// </summary>
    Task<TriggerFileCenterExportDto> TriggerFileCenterExportAsync(Guid tenantId);
}

public class RemoteTenantEchoDto
{
    public Guid? CurrentTenantId { get; set; }

    public string? CurrentTenantName { get; set; }

    public string? HeaderTenant { get; set; }

    public bool IsAuthenticated { get; set; }

    public long? UserId { get; set; }

    public string? Note { get; set; }
}

public class RemoteTenantFactoryProbeDto
{
    public Guid? AmbientTenantIdAfterChange { get; set; }

    public string FactoryType { get; set; } = string.Empty;

    public bool HasReplaceServicesAttribute { get; set; }

    public bool OutboundHasTenantHeader { get; set; }

    public string? OutboundTenantHeaderValue { get; set; }

    public string? Note { get; set; }
}

public class RemoteTenantRoundTripDto
{
    public RemoteTenantFactoryProbeDto Outbound { get; set; } = new();

    public RemoteTenantEchoDto? Inbound { get; set; }

    public string? EchoRequestUrl { get; set; }

    public int? EchoHttpStatus { get; set; }

    public string? Error { get; set; }
}

public class TriggerFileCenterExportDto
{
    public int HttpStatus { get; set; }

    public string? ResponseBody { get; set; }

    public string? Error { get; set; }

    public string? TokenHint { get; set; }
}

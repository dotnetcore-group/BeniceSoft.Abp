namespace BeniceSoft.Abp.Sample.Application.Contracts;

public class TenantDbInfoDto
{
    public Guid? TenantId { get; set; }

    public string? TenantName { get; set; }

    /// <summary>当前实际连接到的 PostgreSQL 数据库名（分片下为 VDS 默认库）。</summary>
    public string DatabaseName { get; set; } = string.Empty;

    public bool IsHost { get; set; }

    /// <summary>说明：分片 DbContext 与 ABP 连接串拆库的关系。</summary>
    public string? Note { get; set; }
}

public interface ITenantIsolationAppService
{
    /// <summary>查看当前逻辑租户与实际物理库（请求头 <c>__tenant</c>）。</summary>
    Task<TenantDbInfoDto> GetCurrentDatabaseAsync();

    /// <summary>同 <see cref="GetCurrentDatabaseAsync"/>（不再演示按租户换库写入）。</summary>
    Task<TenantDbInfoDto> WriteProbeAsync(string? code = null);
}

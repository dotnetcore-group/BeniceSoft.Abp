namespace BeniceSoft.Abp.Sample.Application.Contracts;

/// <summary>
/// 探测 UserPermission：中间件初始化后，SaveChanges / 读 Accessor 时是否仍未初始化。
/// </summary>
public interface IUserPermissionProbeAppService
{
    /// <summary>
    /// 快照当前请求：Accessor / Features / 认证信息（不写库）。
    /// </summary>
    Task<UserPermissionProbeDto> SnapshotAsync();

    /// <summary>
    /// 快照 → 一次 DbContext.SaveChanges → 再快照，对比前后 IsInitialized。
    /// </summary>
    Task<UserPermissionProbeRoundDto> SnapshotAroundSaveChangesAsync();
}

public class UserPermissionProbeDto
{
    public bool IsAuthenticated { get; set; }

    public long? UserId { get; set; }

    public string? ClientId { get; set; }

    public bool AccessorIsInitialized { get; set; }

    public long? AccessorUserId { get; set; }

    public bool FeaturesHasUserPermission { get; set; }

    public bool FeaturesIsInitialized { get; set; }

    public long? FeaturesUserId { get; set; }

    public bool AccessorEqualsFeatures { get; set; }

    /// <summary>DI 解析到的 Accessor 实现类型（排查双程序集加载）。</summary>
    public string? AccessorType { get; set; }

    public string? Note { get; set; }
}

public class UserPermissionProbeRoundDto
{
    public UserPermissionProbeDto BeforeSaveChanges { get; set; } = new();

    public UserPermissionProbeDto AfterSaveChanges { get; set; } = new();

    public int SaveChangesAffected { get; set; }

    public string? Error { get; set; }
}

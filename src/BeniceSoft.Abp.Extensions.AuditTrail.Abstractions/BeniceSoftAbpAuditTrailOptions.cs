namespace BeniceSoft.Abp.Extensions.AuditTrail.Abstractions;

/// <summary>
/// 数据变更审计追踪配置
/// </summary>
public class BeniceSoftAbpAuditTrailOptions
{
    /// <summary>
    /// 是否启用变更采集，默认 false
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 需要排除追踪的实体类型名称列表
    /// </summary>
    public HashSet<string> ExcludedEntityTypes { get; set; } = [];
}


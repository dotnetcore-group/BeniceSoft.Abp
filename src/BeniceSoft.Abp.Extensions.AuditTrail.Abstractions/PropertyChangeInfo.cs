namespace BeniceSoft.Abp.Extensions.AuditTrail.Abstractions;

/// <summary>
/// 属性变更信息
/// </summary>
[Serializable]
public class PropertyChangeInfo
{
    /// <summary>
    /// 属性名称
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称（来自 AuditTrackedAttribute.DisplayName）
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 原始值
    /// </summary>
    public string? OriginalValue { get; set; }

    /// <summary>
    /// 新值
    /// </summary>
    public string? NewValue { get; set; }
}


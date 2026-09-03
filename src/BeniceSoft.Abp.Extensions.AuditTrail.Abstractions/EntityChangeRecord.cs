namespace BeniceSoft.Abp.Extensions.AuditTrail.Abstractions;

/// <summary>
/// 实体变更记录
/// </summary>
[Serializable]
public class EntityChangeRecord
{
    /// <summary>
    /// 变更时间
    /// </summary>
    public DateTimeOffset ChangeTime { get; set; }

    /// <summary>
    /// 实体类型名称
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// 实体Id
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// 变更类型：Added / Modified / Deleted
    /// </summary>
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>
    /// 操作人Id
    /// </summary>
    public long? OperatorId { get; set; }

    /// <summary>
    /// 操作人姓名
    /// </summary>
    public string? OperatorName { get; set; }

    /// <summary>
    /// 属性变更列表
    /// </summary>
    public List<PropertyChangeInfo> Changes { get; set; } = [];
}


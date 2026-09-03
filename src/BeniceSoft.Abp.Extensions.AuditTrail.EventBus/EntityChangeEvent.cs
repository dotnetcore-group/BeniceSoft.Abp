using Volo.Abp.EventBus;

namespace BeniceSoft.Abp.Extensions.AuditTrail.EventBus;

/// <summary>
/// 实体变更分布式事件
/// </summary>
[EventName("BeniceSoft.KA.EntityChangeEvent")]
public class EntityChangeEvent
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
    public List<PropertyChangeDetail> Changes { get; set; } = [];
}

/// <summary>
/// 属性变更明细（ETO）
/// </summary>
public class PropertyChangeDetail
{
    /// <summary>
    /// 属性名称
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
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


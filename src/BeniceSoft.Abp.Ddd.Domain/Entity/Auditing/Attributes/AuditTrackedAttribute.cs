namespace BeniceSoft.Abp.Ddd.Domain.Entity;

/// <summary>
/// 标记需要进行变更追踪的属性
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class AuditTrackedAttribute : Attribute
{
    /// <summary>
    /// 字段显示名称，用于变更记录的可读性
    /// 如果不设置，则使用属性名
    /// </summary>
    public string? DisplayName { get; set; }
}


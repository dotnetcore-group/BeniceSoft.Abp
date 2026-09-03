using BeniceSoft.Abp.Ddd.Domain.Entity;

namespace BeniceSoft.Abp.Extensions.AuditTrail.Tests.TestEntities;

/// <summary>
/// 带 [AuditTracked] 标记的测试实体
/// </summary>
public class TestProduct
{
    public long Id { get; set; }

    [AuditTracked(DisplayName = "产品名称")]
    public string Name { get; set; } = string.Empty;

    [AuditTracked(DisplayName = "价格")]
    public decimal Price { get; set; }

    [AuditTracked]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 没有标记 [AuditTracked]，不应被追踪
    /// </summary>
    public string InternalRemark { get; set; } = string.Empty;

    /// <summary>
    /// 没有标记 [AuditTracked]，不应被追踪
    /// </summary>
    public DateTimeOffset LastSyncTime { get; set; }
}


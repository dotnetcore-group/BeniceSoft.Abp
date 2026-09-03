using BeniceSoft.Abp.Ddd.Domain.Entity;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests.Entities;

/// <summary>
/// 带完整审计字段的测试订单实体
/// 使用 BeniceSoft 自定义审计字段（long 类型的 CreatorId）
/// </summary>
public class TestAuditedOrder : BeniceSoftFullAuditedEntity<long>, IHaveOwnerId
{
    public string OrderNo { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>
    /// 所有者ID
    /// </summary>
    public long OwnerId { get; set; }

    public TestAuditedOrder()
    {
    }

    public TestAuditedOrder(long id, string orderNo, decimal amount)
    {
        Id = id;
        OrderNo = orderNo;
        Amount = amount;
    }
}


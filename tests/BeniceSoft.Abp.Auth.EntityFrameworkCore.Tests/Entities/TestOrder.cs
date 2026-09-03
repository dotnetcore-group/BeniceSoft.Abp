using BeniceSoft.Abp.Ddd.Domain.Entity;
using Volo.Abp.Domain.Entities;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests.Entities;

/// <summary>
/// 订单状态枚举
/// </summary>
public enum OrderStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Cancelled = 4,
    Refunded = 5
}

/// <summary>
/// 测试订单实体
/// </summary>
public class TestOrder : Entity<long>, IHaveOwnerId
{
    public string OrderNo { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 订单状态枚举
    /// </summary>
    public OrderStatus OrderState { get; set; }

    /// <summary>
    /// 部门ID
    /// </summary>
    public long DepartmentId { get; set; }

    /// <summary>
    /// 所有者ID - 必须是 long 类型以实现 IHaveOwnerId 接口
    /// </summary>
    public long OwnerId { get; set; }

    public decimal Amount { get; set; }

    public TestOrder()
    {
    }

    public TestOrder(long id, string orderNo, string status, OrderStatus orderState, long departmentId, long ownerId, decimal amount)
    {
        Id = id;
        OrderNo = orderNo;
        Status = status;
        OrderState = orderState;
        DepartmentId = departmentId;
        OwnerId = ownerId;
        Amount = amount;
    }
}


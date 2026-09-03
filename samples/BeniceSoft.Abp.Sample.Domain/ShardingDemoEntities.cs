using BeniceSoft.Abp.Ddd.Domain.Entity;
using Volo.Abp.Domain.Entities;

namespace BeniceSoft.Abp.Sample.Domain;

/// <summary>
/// 订单（唯一启用分表的实体）。
/// 分片键：OrderTime → 物理表 sales_orders_yyyyMM（按月）。
/// </summary>
public class SalesOrder : BeniceSoftAuditedMultiTenantEntity<Guid>
{
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>关联商品编码（示例不做跨表外键，避免分片表与普通表硬 FK）。</summary>
    public string ProductCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>分片键：下单时间（按月落物理表）。已落库行不要改此字段。</summary>
    public DateTime OrderTime { get; set; }

    public string BatchTag { get; set; } = string.Empty;

    public SalesOrder()
    {
    }

    public SalesOrder(Guid id, string orderNo, string productCode, decimal amount, DateTime orderTime, string batchTag)
    {
        Id = id;
        OrderNo = orderNo;
        ProductCode = productCode;
        Amount = amount;
        OrderTime = orderTime;
        BatchTag = batchTag;
    }
}

/// <summary>
/// 商品主数据（普通表，不分片）。
/// </summary>
public class Product : Entity<Guid>
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public Product()
    {
    }

    public Product(Guid id, string code, string name, decimal unitPrice)
    {
        Id = id;
        Code = code;
        Name = name;
        UnitPrice = unitPrice;
    }
}

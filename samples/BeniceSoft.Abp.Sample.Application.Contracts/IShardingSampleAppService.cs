namespace BeniceSoft.Abp.Sample.Application.Contracts;

public class SalesOrderRowDto
{
    public Guid Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime OrderTime { get; set; }
    public string BatchTag { get; set; } = string.Empty;
}

public class ProductRowDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
}

public class OrderShardingDemoResultDto
{
    public string Operation { get; set; } = string.Empty;
    public string BatchTag { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<SalesOrderRowDto> Orders { get; set; } = [];
    public List<ProductRowDto> Products { get; set; } = [];
}

/// <summary>
/// 订单服务分片示例：只分 sales_orders，商品等普通表。
/// 路由前缀：/api/sample/sharding-sample/...
/// </summary>
public interface IShardingSampleAppService
{
    /// <summary>按月写入 1 月 / 2 月订单，再按 OrderTime 自动路由查询。</summary>
    Task<OrderShardingDemoResultDto> OrderMonthAutoRouteAsync();

    /// <summary>AsRoute Must 钉死物理尾缀（正确 / 错误各查一次）。</summary>
    Task<OrderShardingDemoResultDto> OrderAsRouteMustAsync();

    /// <summary>无 OrderTime 谓词：跨月扇出合并。</summary>
    Task<OrderShardingDemoResultDto> OrderFanOutMergeAsync();

    /// <summary>
    /// 同一次业务：普通表 Product + 分表 SalesOrder 一起写、一起查。
    /// 证明「只分订单」时其它实体仍正常。
    /// </summary>
    Task<OrderShardingDemoResultDto> OrderWithNormalProductAsync();

    /// <summary>
    /// 分表 BulkInsert 示例：应用层调用 <c>ISalesOrderRepository.BulkInsertAsync</c>
    ///（仓储内按分片键分组写入物理表 COPY）。不要对壳 DbContext 直接 Bulk 分表实体。
    /// </summary>
    Task<OrderShardingDemoResultDto> OrderBulkInsertAsync(int perMonth = 50);

    /// <summary>
    /// 分表 BulkUpdate 示例：查批次 → 改 Amount（不改 OrderTime）→ <c>ISalesOrderRepository.BulkUpdateAsync</c>。
    /// </summary>
    Task<OrderShardingDemoResultDto> OrderBulkUpdateAsync(string batchTag, decimal amount = 9.9m);
}

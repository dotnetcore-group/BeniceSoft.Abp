using Volo.Abp.Domain.Repositories;

namespace BeniceSoft.Abp.Sample.Domain;

/// <summary>
/// 订单仓储（分表实体）
/// </summary>
public interface ISalesOrderRepository : IRepository<SalesOrder, Guid>
{
    /// <summary>
    /// 按分片键分组后批量插入到各物理分片表（Npgsql COPY）。
    /// <para>
    /// 不能对壳 DbContext 直接 BulkInsert：Model 表名仍是逻辑表 <c>sales_orders</c>。
    /// </para>
    /// </summary>
    Task<int> BulkInsertAsync(IEnumerable<SalesOrder> orders, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按分片键分组后批量更新各物理分片表。分片键（OrderTime / TenantId）不得修改。
    /// </summary>
    Task<int> BulkUpdateAsync(IEnumerable<SalesOrder> orders, CancellationToken cancellationToken = default);
}

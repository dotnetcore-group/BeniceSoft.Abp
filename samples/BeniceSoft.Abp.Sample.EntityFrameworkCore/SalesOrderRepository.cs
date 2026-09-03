using BeniceSoft.Abp.EntityFrameworkCore.PostgreSql;
using BeniceSoft.Abp.EntityFrameworkCore.Sharding;
using BeniceSoft.Abp.Sample.Domain;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace BeniceSoft.Abp.Sample.EntityFrameworkCore;

/// <summary>
/// 订单仓储：分片 Bulk 仓储实现
/// <para>
/// 正确路径：按分片键分组 → <see cref="IShardingDbContextExecutor.Create{T}"/> 拿物理 DbContext →
/// 对该 Context 调 <c>BulkInsertAsync</c>/<c>BulkUpdateAsync</c>（Npgsql COPY）。
/// </para>
/// </summary>
public class SalesOrderRepository : EfCoreRepository<SampleDbContext, SalesOrder, Guid>, ISalesOrderRepository
{
    public SalesOrderRepository(IDbContextProvider<SampleDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<int> BulkInsertAsync(
        IEnumerable<SalesOrder> orders,
        CancellationToken cancellationToken = default)
    {
        var shell = await GetDbContextAsync();
        var executor = shell.GetExecutor();
        var total = 0;

        foreach (var group in orders.GroupBy(RouteKey).OrderBy(g => g.Key.Month).ThenBy(g => g.Key.TenantId))
        {
            var items = group.ToList();
            if (items.Count == 0)
            {
                continue;
            }

            // Create(entity) 按 TenantId + OrderTime 路由到物理库/月表，Model.GetTableName() 已是物理表名
            var physical = executor.Create(items[0]);
            total += await physical.BulkInsertAsync(
                items,
                atom => atom.WithCommandTimeout(120),
                cancellationToken);
        }

        return total;
    }

    public virtual async Task<int> BulkUpdateAsync(
        IEnumerable<SalesOrder> orders,
        CancellationToken cancellationToken = default)
    {
        var shell = await GetDbContextAsync();
        var executor = shell.GetExecutor();
        var total = 0;

        foreach (var group in orders.GroupBy(RouteKey).OrderBy(g => g.Key.Month).ThenBy(g => g.Key.TenantId))
        {
            var items = group.ToList();
            if (items.Count == 0)
            {
                continue;
            }

            var physical = executor.Create(items[0]);
            total += await physical.BulkUpdateAsync(
                items,
                atom => atom.WithCommandTimeout(120),
                matchBuilder: null,
                cancellationToken);
        }

        return total;
    }

    /// <summary>与分库（TenantId）+ 分表（OrderTime 月）路由键对齐，避免跨租户/跨月混进同一物理 Context。</summary>
    private static (Guid TenantId, DateTime Month) RouteKey(SalesOrder order)
        => (order.TenantId ?? Guid.Empty, new DateTime(order.OrderTime.Year, order.OrderTime.Month, 1));
}

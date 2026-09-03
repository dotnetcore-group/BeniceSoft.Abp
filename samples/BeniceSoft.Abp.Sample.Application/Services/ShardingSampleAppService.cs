using BeniceSoft.Abp.EntityFrameworkCore.Sharding;
using BeniceSoft.Abp.Sample.Application.Contracts;
using BeniceSoft.Abp.Sample.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.Sample.Application.Services;

/// <summary>
/// 订单服务分片示例（匿名联调）：只分订单表，商品等普通表。
/// 路由：/api/sample/sharding-sample/...
/// </summary>
[AllowAnonymous]
public class ShardingSampleAppService : SampleAppServiceBase, IShardingSampleAppService
{
    private readonly ISalesOrderRepository _orderRepo;
    private readonly IRepository<Product, Guid> _productRepo;
    private readonly IUnitOfWorkManager _uowManager;

    public ShardingSampleAppService(
        ISalesOrderRepository orderRepo,
        IRepository<Product, Guid> productRepo,
        IUnitOfWorkManager uowManager)
    {
        _orderRepo = orderRepo;
        _productRepo = productRepo;
        _uowManager = uowManager;
    }

    public virtual async Task<OrderShardingDemoResultDto> OrderMonthAutoRouteAsync()
    {
        var jan = new DateTime(2024, 1, 15);
        var feb = new DateTime(2024, 2, 20);
        var batch = NewBatch("month");

        using (var uow = _uowManager.Begin(requiresNew: true, isTransactional: true))
        {
            await _orderRepo.InsertAsync(new SalesOrder(Guid.NewGuid(), "SO-JAN", "SKU-A", 100m, jan, batch), autoSave: true);
            await _orderRepo.InsertAsync(new SalesOrder(Guid.NewGuid(), "SO-FEB", "SKU-B", 200m, feb, batch), autoSave: true);
            await uow.CompleteAsync();
        }

        using (var uow = _uowManager.Begin(requiresNew: true, isTransactional: true))
        {
            var janRows = await (await _orderRepo.GetQueryableAsync())
                .AsNoTracking()
                .Where(x => x.OrderTime == jan && x.BatchTag == batch)
                .ToListAsync();
            var febRows = await (await _orderRepo.GetQueryableAsync())
                .AsNoTracking()
                .Where(x => x.OrderTime == feb && x.BatchTag == batch)
                .ToListAsync();
            await uow.CompleteAsync();

            return new OrderShardingDemoResultDto
            {
                Operation = "OrderMonthAutoRoute",
                BatchTag = batch,
                Message = "Where(OrderTime==…) 自动进 sales_orders_yyyyMM；商品表未分片",
                Orders = janRows.Concat(febRows).Select(ToOrderDto).ToList()
            };
        }
    }

    public virtual async Task<OrderShardingDemoResultDto> OrderAsRouteMustAsync()
    {
        var jan = new DateTime(2024, 1, 10);
        var batch = NewBatch("must");
        var id = Guid.NewGuid();

        using (var uow = _uowManager.Begin(requiresNew: true, isTransactional: true))
        {
            await _orderRepo.InsertAsync(new SalesOrder(id, "SO-PIN", "SKU-A", 1m, jan, batch), autoSave: true);
            await uow.CompleteAsync();
        }

        using (var uow = _uowManager.Begin(requiresNew: true, isTransactional: true))
        {
            var wrong = await (await _orderRepo.GetQueryableAsync())
                .AsNoTracking()
                .AsRoute(ctx => ctx.MustTable[typeof(SalesOrder)] = new HashSet<string> { "202402" })
                .Where(x => x.Id == id)
                .ToListAsync();

            var pinned = await (await _orderRepo.GetQueryableAsync())
                .AsNoTracking()
                .AsRoute(ctx => ctx.MustTable[typeof(SalesOrder)] = new HashSet<string> { "202401" })
                .Where(x => x.Id == id)
                .ToListAsync();
            await uow.CompleteAsync();

            return new OrderShardingDemoResultDto
            {
                Operation = "OrderAsRouteMust",
                BatchTag = batch,
                Message = $"Must 202402 → {wrong.Count} 行；Must 202401 → {pinned.Count} 行",
                Orders = pinned.Select(ToOrderDto).ToList()
            };
        }
    }

    public virtual async Task<OrderShardingDemoResultDto> OrderFanOutMergeAsync()
    {
        var batch = NewBatch("fan");
        using (var uow = _uowManager.Begin(requiresNew: true, isTransactional: true))
        {
            await _orderRepo.InsertAsync(new SalesOrder(Guid.NewGuid(), "SO-A", "SKU-A", 1m, new DateTime(2024, 1, 5), batch), autoSave: true);
            await _orderRepo.InsertAsync(new SalesOrder(Guid.NewGuid(), "SO-B", "SKU-B", 2m, new DateTime(2024, 2, 5), batch), autoSave: true);
            await uow.CompleteAsync();
        }

        using (var uow = _uowManager.Begin(requiresNew: true, isTransactional: true))
        {
            var rows = await (await _orderRepo.GetQueryableAsync())
                .AsNoTracking()
                .Where(x => x.BatchTag == batch)
                .OrderBy(x => x.OrderTime)
                .ToListAsync();
            await uow.CompleteAsync();

            return new OrderShardingDemoResultDto
            {
                Operation = "OrderFanOutMerge",
                BatchTag = batch,
                Message = "无 OrderTime 谓词时跨月扇出合并；行数应=2",
                Orders = rows.Select(ToOrderDto).ToList()
            };
        }
    }

    public virtual async Task<OrderShardingDemoResultDto> OrderWithNormalProductAsync()
    {
        var batch = NewBatch("mix");
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderTime = new DateTime(2024, 3, 8);

        using (var uow = _uowManager.Begin(requiresNew: true, isTransactional: true))
        {
            await _productRepo.InsertAsync(new Product(productId, $"SKU-{batch}", "混合场景商品", 99m), autoSave: true);
            await _orderRepo.InsertAsync(
                new SalesOrder(orderId, "SO-MIX", $"SKU-{batch}", 99m, orderTime, batch),
                autoSave: true);
            await uow.CompleteAsync();
        }

        using (var uow = _uowManager.Begin(requiresNew: true, isTransactional: true))
        {
            var products = await (await _productRepo.GetQueryableAsync())
                .AsNoTracking()
                .Where(x => x.Id == productId)
                .ToListAsync();
            var orders = await (await _orderRepo.GetQueryableAsync())
                .AsNoTracking()
                .Where(x => x.Id == orderId && x.OrderTime == orderTime)
                .ToListAsync();
            await uow.CompleteAsync();

            return new OrderShardingDemoResultDto
            {
                Operation = "OrderWithNormalProduct",
                BatchTag = batch,
                Message = "同 UoW：products（普通表）+ sales_orders_202403（分表）均可读写",
                Products = products.Select(ToProductDto).ToList(),
                Orders = orders.Select(ToOrderDto).ToList()
            };
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 应用层只调 <see cref="ISalesOrderRepository.BulkInsertAsync"/>；
    /// 按月/租户分组、拿物理 DbContext、COPY 写入都在仓储实现里（与 AM 等业务自定义仓储一致）。
    /// </remarks>
    [UnitOfWork]
    public virtual async Task<OrderShardingDemoResultDto> OrderBulkInsertAsync(int perMonth = 50)
    {
        if (perMonth < 1)
        {
            perMonth = 1;
        }

        if (perMonth > 5000)
        {
            perMonth = 5000;
        }

        var batch = NewBatch("bulk");
        var jan = new DateTime(2024, 1, 15);
        var feb = new DateTime(2024, 2, 20);
        var orders = new List<SalesOrder>(perMonth * 2);
        for (var i = 0; i < perMonth; i++)
        {
            orders.Add(new SalesOrder(Guid.NewGuid(), $"SO-J{i:D4}", "SKU-A", 10m + i, jan, batch));
            orders.Add(new SalesOrder(Guid.NewGuid(), $"SO-F{i:D4}", "SKU-B", 20m + i, feb, batch));
        }

        var affected = await _orderRepo.BulkInsertAsync(orders);

        var rows = await (await _orderRepo.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.BatchTag == batch)
            .OrderBy(x => x.OrderTime)
            .ThenBy(x => x.OrderNo)
            .Take(6)
            .ToListAsync();

        return new OrderShardingDemoResultDto
        {
            Operation = "OrderBulkInsert",
            BatchTag = batch,
            Message = $"ISalesOrderRepository.BulkInsertAsync affected={affected}（{perMonth}×2 月）；抽样 {rows.Count} 行 → sales_orders_202401 / _202402",
            Orders = rows.Select(ToOrderDto).ToList()
        };
    }

    /// <inheritdoc />
    [UnitOfWork]
    public virtual async Task<OrderShardingDemoResultDto> OrderBulkUpdateAsync(string batchTag, decimal amount = 9.9m)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(batchTag);

        // 跨月查询走分片扇出；不要改 OrderTime（分片键）
        var items = await (await _orderRepo.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.BatchTag == batchTag)
            .ToListAsync();

        if (items.Count == 0)
        {
            return new OrderShardingDemoResultDto
            {
                Operation = "OrderBulkUpdate",
                BatchTag = batchTag,
                Message = "未找到批次数据，请先调用 OrderBulkInsertAsync",
                Orders = []
            };
        }

        foreach (var item in items)
        {
            item.Amount = amount;
            // item.OrderTime = ...  // 禁止：分片键只允许插入时赋值
        }

        var affected = await _orderRepo.BulkUpdateAsync(items);

        var rows = await (await _orderRepo.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.BatchTag == batchTag)
            .OrderBy(x => x.OrderTime)
            .Take(6)
            .ToListAsync();

        return new OrderShardingDemoResultDto
        {
            Operation = "OrderBulkUpdate",
            BatchTag = batchTag,
            Message = $"ISalesOrderRepository.BulkUpdateAsync affected={affected}；Amount={amount}，OrderTime 未改",
            Orders = rows.Select(ToOrderDto).ToList()
        };
    }

    private static string NewBatch(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..24];

    private static SalesOrderRowDto ToOrderDto(SalesOrder x) => new()
    {
        Id = x.Id,
        OrderNo = x.OrderNo,
        ProductCode = x.ProductCode,
        Amount = x.Amount,
        OrderTime = x.OrderTime,
        BatchTag = x.BatchTag
    };

    private static ProductRowDto ToProductDto(Product x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        UnitPrice = x.UnitPrice
    };
}

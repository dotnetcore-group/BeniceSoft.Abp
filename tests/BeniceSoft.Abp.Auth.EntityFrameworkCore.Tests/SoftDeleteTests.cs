using BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Uow;
using Xunit;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests;

/// <summary>
/// 软删除功能测试
/// 验证 IBeniceSoftFullAudited 实体的软删除过滤器是否正常工作
/// </summary>
public class SoftDeleteTests : AuthEfCoreTestBase
{
    /// <summary>
    /// 初始化带审计字段的测试数据
    /// </summary>
    private async Task SeedAuditedTestDataAsync()
    {
        // 设置当前用户
        MockCurrentUser.Id = 1001;
        MockCurrentUser.IsAuthenticated = true;

        using var uow = UnitOfWorkManager.Begin();

        // 清空现有数据（包括软删除的）- 直接使用 DbContext 硬删除
        using (DataFilter.Disable<ISoftDelete>())
        {
            var existingOrders = await AuditedOrderRepository.GetListAsync();
            foreach (var order in existingOrders)
            {
                DbContext.TestAuditedOrders.Remove(order);
            }
            await DbContext.SaveChangesAsync();
        }

        // 插入测试数据
        await AuditedOrderRepository.InsertAsync(new TestAuditedOrder(1, "AUD001", 100.00m));
        await AuditedOrderRepository.InsertAsync(new TestAuditedOrder(2, "AUD002", 200.00m));
        await AuditedOrderRepository.InsertAsync(new TestAuditedOrder(3, "AUD003", 300.00m));

        await uow.CompleteAsync();
    }

    [Fact]
    public async Task Delete_ShouldSetIsDeletedToTrue_AndNotPhysicallyDelete()
    {
        // Arrange
        await SeedAuditedTestDataAsync();

        // Act - 软删除一条记录
        using (var uow = UnitOfWorkManager.Begin())
        {
            var order = await AuditedOrderRepository.GetAsync(1);
            await AuditedOrderRepository.DeleteAsync(order);
            await uow.CompleteAsync();
        }

        // Assert - 正常查询应该查不到被删除的记录
        using (var uow = UnitOfWorkManager.Begin())
        {
            var orders = await AuditedOrderRepository.GetListAsync();
            orders.Count.ShouldBe(2);
            orders.ShouldNotContain(x => x.Id == 1);
            await uow.CompleteAsync();
        }

        // Assert - 禁用软删除过滤器后应该能查到
        using (var uow = UnitOfWorkManager.Begin())
        {
            using (DataFilter.Disable<ISoftDelete>())
            {
                var allOrders = await AuditedOrderRepository.GetListAsync();
                allOrders.Count.ShouldBe(3);

                var deletedOrder = allOrders.First(x => x.Id == 1);
                deletedOrder.IsDeleted.ShouldBeTrue();
                // 注意：DeletionTime 和 DeleterId 需要 BeniceSoftAbpDbContext 正确处理软删除
                // 如果这些断言失败，说明软删除审计字段没有被正确设置
                deletedOrder.DeletionTime.ShouldNotBeNull($"DeletionTime should be set. IsDeleted={deletedOrder.IsDeleted}");
                deletedOrder.DeleterId.ShouldBe(1001, $"DeleterId should be set. Actual={deletedOrder.DeleterId}");
            }
            await uow.CompleteAsync();
        }
    }

    [Fact]
    public async Task GetListAsync_ShouldNotReturnSoftDeletedRecords_ByDefault()
    {
        // Arrange
        await SeedAuditedTestDataAsync();

        // 软删除两条记录
        using (var uow = UnitOfWorkManager.Begin())
        {
            var order1 = await AuditedOrderRepository.GetAsync(1);
            var order2 = await AuditedOrderRepository.GetAsync(2);
            await AuditedOrderRepository.DeleteAsync(order1);
            await AuditedOrderRepository.DeleteAsync(order2);
            await uow.CompleteAsync();
        }

        // Act & Assert - 默认查询不应返回软删除的记录
        using (var uow = UnitOfWorkManager.Begin())
        {
            var orders = await AuditedOrderRepository.GetListAsync();
            orders.Count.ShouldBe(1);
            orders.First().Id.ShouldBe(3);
            await uow.CompleteAsync();
        }
    }

    [Fact]
    public async Task DisableSoftDeleteFilter_ShouldReturnAllRecords_IncludingDeleted()
    {
        // Arrange
        await SeedAuditedTestDataAsync();

        // 软删除一条记录
        using (var uow = UnitOfWorkManager.Begin())
        {
            var order = await AuditedOrderRepository.GetAsync(2);
            await AuditedOrderRepository.DeleteAsync(order);
            await uow.CompleteAsync();
        }

        // Act & Assert - 禁用过滤器后应返回所有记录
        using (var uow = UnitOfWorkManager.Begin())
        {
            using (DataFilter.Disable<ISoftDelete>())
            {
                var allOrders = await AuditedOrderRepository.GetListAsync();
                allOrders.Count.ShouldBe(3);

                var deletedCount = allOrders.Count(x => x.IsDeleted);
                deletedCount.ShouldBe(1);

                var notDeletedCount = allOrders.Count(x => !x.IsDeleted);
                notDeletedCount.ShouldBe(2);
            }
            await uow.CompleteAsync();
        }
    }

    [Fact]
    public async Task FindAsync_ShouldReturnNull_ForSoftDeletedRecord()
    {
        // Arrange
        await SeedAuditedTestDataAsync();

        // 软删除记录
        using (var uow = UnitOfWorkManager.Begin())
        {
            var order = await AuditedOrderRepository.GetAsync(1);
            await AuditedOrderRepository.DeleteAsync(order);
            await uow.CompleteAsync();
        }

        // Act & Assert - FindAsync 应该返回 null
        using (var uow = UnitOfWorkManager.Begin())
        {
            var order = await AuditedOrderRepository.FindAsync(1);
            order.ShouldBeNull();
            await uow.CompleteAsync();
        }
    }
}


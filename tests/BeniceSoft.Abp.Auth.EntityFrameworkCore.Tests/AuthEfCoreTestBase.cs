using Microsoft.Extensions.DependencyInjection;
using BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests.Entities;
using BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests.Mocks;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Testing;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests;

public abstract class AuthEfCoreTestBase : AbpIntegratedTest<AuthEfCoreTestModule>
{
    protected IRepository<TestOrder, long> OrderRepository => GetRequiredService<IRepository<TestOrder, long>>();
    protected IRepository<TestAuditedOrder, long> AuditedOrderRepository => GetRequiredService<IRepository<TestAuditedOrder, long>>();
    protected MockCurrentUserPermissionAccessor PermissionAccessor => GetRequiredService<MockCurrentUserPermissionAccessor>();
    protected MockBeniceSoftCurrentUser MockCurrentUser => GetRequiredService<MockBeniceSoftCurrentUser>();
    protected IUnitOfWorkManager UnitOfWorkManager => GetRequiredService<IUnitOfWorkManager>();
    protected IDataFilter DataFilter => GetRequiredService<IDataFilter>();
    protected TestDbContext DbContext => GetRequiredService<TestDbContext>();

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    /// <summary>
    /// 初始化测试数据
    /// </summary>
    protected async Task SeedTestDataAsync()
    {
        using var uow = UnitOfWorkManager.Begin();

        // 清空现有数据
        var existingOrders = await OrderRepository.GetListAsync();
        foreach (var order in existingOrders)
        {
            await OrderRepository.DeleteAsync(order);
        }

        // 插入测试数据
        // OrderState: Pending=1, Processing=2, Completed=3, Cancelled=4, Refunded=5
        await OrderRepository.InsertAsync(new TestOrder(1, "ORD001", "Pending", OrderStatus.Pending, 100, 1001, 100.00m));
        await OrderRepository.InsertAsync(new TestOrder(2, "ORD002", "Completed", OrderStatus.Completed, 100, 1002, 200.00m));
        await OrderRepository.InsertAsync(new TestOrder(3, "ORD003", "Pending", OrderStatus.Processing, 200, 1001, 300.00m));
        await OrderRepository.InsertAsync(new TestOrder(4, "ORD004", "Cancelled", OrderStatus.Cancelled, 200, 1003, 400.00m));
        await OrderRepository.InsertAsync(new TestOrder(5, "ORD005", "Completed", OrderStatus.Refunded, 300, 1002, 500.00m));

        await uow.CompleteAsync();
    }

    /// <summary>
    /// 清除权限设置
    /// </summary>
    protected void ClearPermission()
    {
        PermissionAccessor.UserPermission = null;
    }
}


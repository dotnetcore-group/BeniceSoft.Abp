using Microsoft.Extensions.DependencyInjection;
using BeniceSoft.Abp.Auth.Core.Models;
using BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests.Entities;
using BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests.Mocks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests;

public class ColumnPermissionInterceptorTests : AuthEfCoreTestBase
{
    private readonly IRepository<TestOrder, long> _repository;
    private readonly MockCurrentUserPermissionAccessor _permissionAccessor;

    public ColumnPermissionInterceptorTests()
    {
        _repository = ServiceProvider.GetRequiredService<IRepository<TestOrder, long>>();
        _permissionAccessor = ServiceProvider.GetRequiredService<MockCurrentUserPermissionAccessor>();
    }

    [Fact]
    public async Task Should_Allow_Update_When_No_Column_Permission_Configured()
    {
        var order = new TestOrder(100, "ORD-100", "Pending", OrderStatus.Pending, 1, 1, 100m);
        await _repository.InsertAsync(order);

        order.Amount = 200m;
        order.Status = "Completed";
        await _repository.UpdateAsync(order);

        var updated = await _repository.GetAsync(100);
        updated.Amount.ShouldBe(200m);
        updated.Status.ShouldBe("Completed");
    }

    [Fact]
    public async Task Should_Block_Update_When_Column_Has_ReadOnly_Permission()
    {
        _permissionAccessor.UserPermission = new MockUserPermission
        {
            UserId = 1,
            FieldPermissions =
            [
                new FieldPermission
                {
                    TableName = "test_orders",
                    FieldName = "Amount",
                    FieldAuthLevel = (int)FieldAuthLevelEnum.ReadOnly,
                    IsDisplay = true
                }
            ]
        };

        var order = new TestOrder(101, "ORD-101", "Pending", OrderStatus.Pending, 1, 1, 100m);
        await _repository.InsertAsync(order);

        order.Amount = 999m;
        order.Status = "Completed";
        await _repository.UpdateAsync(order);

        var updated = await _repository.GetAsync(101);
        updated.Amount.ShouldBe(100m);
        updated.Status.ShouldBe("Completed");
    }

    [Fact]
    public async Task Should_Allow_Update_When_Column_Has_ReadWrite_Permission()
    {
        _permissionAccessor.UserPermission = new MockUserPermission
        {
            UserId = 1,
            FieldPermissions =
            [
                new FieldPermission
                {
                    TableName = "test_orders",
                    FieldName = "Amount",
                    FieldAuthLevel = (int)FieldAuthLevelEnum.ReadWrite,
                    IsDisplay = true
                }
            ]
        };

        var order = new TestOrder(102, "ORD-102", "Pending", OrderStatus.Pending, 1, 1, 100m);
        await _repository.InsertAsync(order);

        order.Amount = 500m;
        await _repository.UpdateAsync(order);

        var updated = await _repository.GetAsync(102);
        updated.Amount.ShouldBe(500m);
    }

    [Fact]
    public async Task Should_Block_Update_When_Column_Has_None_Permission()
    {
        _permissionAccessor.UserPermission = new MockUserPermission
        {
            UserId = 1,
            FieldPermissions =
            [
                new FieldPermission
                {
                    TableName = "test_orders",
                    FieldName = "Status",
                    FieldAuthLevel = (int)FieldAuthLevelEnum.None,
                    IsDisplay = false
                }
            ]
        };

        var order = new TestOrder(103, "ORD-103", "Pending", OrderStatus.Pending, 1, 1, 100m);
        await _repository.InsertAsync(order);

        order.Status = "Cancelled";
        order.Amount = 300m;
        await _repository.UpdateAsync(order);

        var updated = await _repository.GetAsync(103);
        updated.Status.ShouldBe("Pending");
        updated.Amount.ShouldBe(300m);
    }

    [Fact]
    public async Task Should_Block_Multiple_Columns_When_All_Have_ReadOnly_Permission()
    {
        _permissionAccessor.UserPermission = new MockUserPermission
        {
            UserId = 1,
            FieldPermissions =
            [
                new FieldPermission
                {
                    TableName = "test_orders",
                    FieldName = "Amount",
                    FieldAuthLevel = (int)FieldAuthLevelEnum.ReadOnly,
                    IsDisplay = true
                },
                new FieldPermission
                {
                    TableName = "test_orders",
                    FieldName = "Status",
                    FieldAuthLevel = (int)FieldAuthLevelEnum.ReadOnly,
                    IsDisplay = true
                }
            ]
        };

        var order = new TestOrder(104, "ORD-104", "Pending", OrderStatus.Pending, 1, 1, 100m);
        await _repository.InsertAsync(order);

        order.Amount = 888m;
        order.Status = "Completed";
        await _repository.UpdateAsync(order);

        var updated = await _repository.GetAsync(104);
        updated.Amount.ShouldBe(100m);
        updated.Status.ShouldBe("Pending");
    }
}


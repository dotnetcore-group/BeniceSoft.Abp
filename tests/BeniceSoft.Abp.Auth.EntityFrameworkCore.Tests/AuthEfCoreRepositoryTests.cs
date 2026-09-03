using BeniceSoft.Abp.Auth.Core.Models;
using BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests.Mocks;
using BeniceSoft.Core;
using Volo.Abp.Uow;
using Xunit;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests;

public class AuthEfCoreRepositoryTests : AuthEfCoreTestBase
{
    [Fact]
    public async Task GetListAsync_WithoutPermission_ShouldReturnAllData()
    {
        // Arrange
        await SeedTestDataAsync();
        ClearPermission();

        // Act
        using var uow = UnitOfWorkManager.Begin();
        var result = await OrderRepository.GetListAsync();
        await uow.CompleteAsync();

        // Assert
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task GetListAsync_WithDepartmentFilter_ShouldReturnFilteredData()
    {
        // Arrange
        await SeedTestDataAsync();

        // 设置权限：只能看到 DepartmentId = 100 的数据
        // 注意：由于 TestOrder 实现了 IHaveOwnerId，BuildRowPermissionPredicate 会自动添加
        // OwnerId == UserId 的过滤条件，并与其他条件进行 OR 运算
        // 所以最终的过滤表达式是：(OwnerId == 1002) OR (DepartmentId IN ["100"])
        // 使用用户 1002，他拥有 ORD002 和 ORD005
        // DepartmentId = 100 的订单是 ORD001 和 ORD002
        // 结果应该是：ORD001（DepartmentId=100）、ORD002（OwnerId=1002 且 DepartmentId=100）、ORD005（OwnerId=1002）
        // 共 3 条记录
        PermissionAccessor.UserPermission = new MockUserPermission
        {
            UserId = 1002,
            RowPermissions =
            [
                new RowPermission
                {
                    TableName = "test_orders",
                    ConditionGroups =
                    [
                        new RowPermissionConditionGroup
                        {
                            LogicalOperator = "and",
                            Conditions =
                            [
                                new RowPermissionCondition
                                {
                                    ColumnName = "DepartmentId",
                                    Operator = ((int)ExprOperator.In).ToString(),
                                    Values = ["100"]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        // Act
        using var uow = UnitOfWorkManager.Begin();
        var result = await OrderRepository.GetListAsync();
        await uow.CompleteAsync();

        // Assert - 用户 1002 拥有 ORD002 和 ORD005，DepartmentId=100 的有 ORD001 和 ORD002
        // 结果应该是 ORD001, ORD002, ORD005 共 3 条
        Assert.Equal(3, result.Count);
        // 验证结果包含正确的订单
        var orderNos = result.Select(o => o.OrderNo).OrderBy(x => x).ToList();
        Assert.Equal(["ORD001", "ORD002", "ORD005"], orderNos);
    }

    [Fact]
    public async Task GetListAsync_WithOwnerIdFilter_ShouldReturnOwnedData()
    {
        // Arrange
        await SeedTestDataAsync();

        // 设置权限：用户 1001 只能看到自己的数据（通过 IHaveOwnerId 自动添加）
        // BuildRowPermissionPredicate 会自动添加 OwnerId == UserId 的过滤条件
        // 同时设置一个不匹配任何数据的条件（DepartmentId IN ["999"]）
        // 最终的过滤表达式是：(OwnerId == 1001) OR (DepartmentId IN ["999"])
        // 由于没有 DepartmentId = "999" 的数据，所以只会返回 OwnerId = 1001 的数据
        PermissionAccessor.UserPermission = new MockUserPermission
        {
            UserId = 1001,
            RowPermissions =
            [
                new RowPermission
                {
                    TableName = "test_orders",
                    ConditionGroups =
                    [
                        new RowPermissionConditionGroup
                        {
                            LogicalOperator = "and",
                            Conditions =
                            [
                                new RowPermissionCondition
                                {
                                    IsDataSuperAdmin = false,
                                    ColumnName = "DepartmentId",
                                    Operator = ((int)ExprOperator.In).ToString(),
                                    Values = ["999"] // 不存在的部门，不会匹配任何数据
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        // Act
        using var uow = UnitOfWorkManager.Begin();
        var result = await OrderRepository.GetListAsync();
        await uow.CompleteAsync();

        // Assert - 用户 1001 拥有 ORD001 和 ORD003
        Assert.Equal(2, result.Count);
        Assert.All(result, order => Assert.Equal(1001, order.OwnerId));
    }

    [Fact]
    public async Task GetListAsync_WithSuperAdminPermission_ShouldReturnAllData()
    {
        // Arrange
        await SeedTestDataAsync();

        // 设置权限：超级管理员
        PermissionAccessor.UserPermission = new MockUserPermission
        {
            UserId = 9999,
            RowPermissions =
            [
                new RowPermission
                {
                    TableName = "test_orders",
                    ConditionGroups =
                    [
                        new RowPermissionConditionGroup
                        {
                            LogicalOperator = "and",
                            Conditions =
                            [
                                new RowPermissionCondition
                                {
                                    IsDataSuperAdmin = true,
                                    ColumnName = "DepartmentId",
                                    Operator = ((int)ExprOperator.In).ToString(),
                                    Values = []
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        // Act
        using var uow = UnitOfWorkManager.Begin();
        var result = await OrderRepository.GetListAsync();
        await uow.CompleteAsync();

        // Assert
        Assert.Equal(5, result.Count);
    }
}


using BeniceSoft.Abp.Auth.Core.Models;
using BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests.Mocks;
using BeniceSoft.Core;
using Volo.Abp.Uow;
using Xunit;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests;

public class AuthEfCoreRepositoryAdvancedTests : AuthEfCoreTestBase
{
    [Fact]
    public async Task GetListAsync_WithMultipleDepartments_ShouldReturnFilteredData()
    {
        // Arrange
        await SeedTestDataAsync();

        // 设置权限：可以看到 DepartmentId = 100 或 200 的数据
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
                                    ColumnName = "DepartmentId",
                                    Operator = ((int)ExprOperator.In).ToString(),
                                    Values = ["100", "200"]
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
        Assert.Equal(4, result.Count);
        Assert.All(result, order => Assert.True(order.DepartmentId == 100 || order.DepartmentId == 200));
    }

    [Fact]
    public async Task GetListAsync_WithNoMatchingPermission_ShouldReturnOnlyOwnedData()
    {
        // Arrange
        await SeedTestDataAsync();

        // 设置权限：没有任何值的条件（非超管）
        // 由于 TestOrder 实现了 IHaveOwnerId，BuildRowPermissionPredicate 会自动添加
        // OwnerId == UserId 的过滤条件
        // 最终的过滤表达式是：(OwnerId == 1001) OR (False)
        // 所以会返回用户 1001 拥有的订单
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
                                    Values = [] // 空值列表
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
    public async Task GetListAsync_WithDifferentTablePermission_ShouldReturnAllData()
    {
        // Arrange
        await SeedTestDataAsync();

        // 设置权限：针对其他表的权限，不影响当前表
        PermissionAccessor.UserPermission = new MockUserPermission
        {
            UserId = 1001,
            RowPermissions =
            [
                new RowPermission
                {
                    TableName = "other_table", // 不同的表名
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

        // Assert - 因为权限是针对其他表的，所以返回所有数据
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task GetCountAsync_WithPermission_ShouldReturnFilteredCount()
    {
        // Arrange
        await SeedTestDataAsync();

        // 设置权限：只能看到 DepartmentId = 100 的数据
        // 由于 TestOrder 实现了 IHaveOwnerId，BuildRowPermissionPredicate 会自动添加
        // OwnerId == UserId 的过滤条件
        // 最终的过滤表达式是：(OwnerId == 1001) OR (DepartmentId IN ["100"])
        // 用户 1001 拥有 ORD001 和 ORD003
        // DepartmentId = 100 的订单是 ORD001 和 ORD002
        // 结果应该是 ORD001, ORD002, ORD003 共 3 条
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
        var count = await OrderRepository.GetCountAsync();
        await uow.CompleteAsync();

        // Assert - 用户 1001 拥有 ORD001 和 ORD003，DepartmentId=100 的有 ORD001 和 ORD002
        // 结果应该是 ORD001, ORD002, ORD003 共 3 条
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetListAsync_WithEnumInFilter_ShouldReturnFilteredData()
    {
        // Arrange
        await SeedTestDataAsync();

        // 测试 In 操作符：OrderState IN [1, 2, 3] (Pending, Processing, Completed)
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
                                    ColumnName = "OrderState",
                                    Operator = ((int)ExprOperator.In).ToString(),
                                    Values = ["1", "2", "3"]
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

        // Assert - ORD001(Pending=1), ORD002(Completed=3), ORD003(Processing=2)
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetListAsync_WithEnumEqualFilter_ShouldReturnFilteredData()
    {
        // Arrange
        await SeedTestDataAsync();

        // 测试 Equal 操作符：OrderState == 3 (Completed)
        // 注意：Equal 操作符只需要一个值
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
                                    ColumnName = "OrderState",
                                    Operator = ((int)ExprOperator.Equal).ToString(),
                                    Values = ["3"] // 单个值
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

        // Assert - 只有 ORD002 (Completed=3)
        Assert.Single(result);
        Assert.Equal("ORD002", result[0].OrderNo);
    }

    [Fact]
    public async Task GetListAsync_WithEnumGreaterThanFilter_ShouldReturnFilteredData()
    {
        // Arrange
        await SeedTestDataAsync();

        // 测试 GreaterThan 操作符：OrderState > 2 (Processing)
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
                                    ColumnName = "OrderState",
                                    Operator = ((int)ExprOperator.GreaterThan).ToString(),
                                    Values = ["2"] // 单个值
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

        // Assert - ORD002(Completed=3), ORD004(Cancelled=4), ORD005(Refunded=5)
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetListAsync_WithEnumLessThanOrEqualFilter_ShouldReturnFilteredData()
    {
        // Arrange
        await SeedTestDataAsync();

        // 测试 LessThanOrEqual 操作符：OrderState <= 2 (Processing)
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
                                    ColumnName = "OrderState",
                                    Operator = ((int)ExprOperator.LessThanOrEqual).ToString(),
                                    Values = ["2"] // 单个值
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

        // Assert - ORD001(Pending=1), ORD003(Processing=2)
        Assert.Equal(2, result.Count);
    }
}


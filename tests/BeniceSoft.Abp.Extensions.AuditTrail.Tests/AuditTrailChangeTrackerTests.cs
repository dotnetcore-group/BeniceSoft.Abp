using BeniceSoft.Abp.EntityFrameworkCore;
using BeniceSoft.Abp.Extensions.AuditTrail.Tests.TestEntities;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace BeniceSoft.Abp.Extensions.AuditTrail.Tests;

public class AuditTrailChangeTrackerTests : IDisposable
{
    private readonly TestDbContext _dbContext;

    public AuditTrailChangeTrackerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TestDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public void CaptureChanges_AddEntity_ShouldCaptureTrackedProperties()
    {
        // Arrange
        var product = new TestProduct
        {
            Id = 1,
            Name = "测试产品",
            Price = 99.9m,
            Status = "Active",
            InternalRemark = "内部备注"
        };
        _dbContext.Products.Add(product);

        // Act
        var changes = AuditTrailChangeTracker.CaptureChanges(_dbContext.ChangeTracker, 1001, "张三");

        // Assert
        changes.Count.ShouldBe(1);
        var record = changes[0];
        record.EntityType.ShouldBe("TestProduct");
        record.EntityId.ShouldBe("1");
        record.ChangeType.ShouldBe("Added");
        record.OperatorId.ShouldBe(1001);
        record.OperatorName.ShouldBe("张三");

        // 应该只有 3 个被标记的属性（Name, Price, Status），不包含 InternalRemark 和 LastSyncTime
        record.Changes.Count.ShouldBe(3);

        var nameChange = record.Changes.Single(c => c.PropertyName == "Name");
        nameChange.DisplayName.ShouldBe("产品名称");
        nameChange.OriginalValue.ShouldBeNull();
        nameChange.NewValue.ShouldBe("测试产品");

        var priceChange = record.Changes.Single(c => c.PropertyName == "Price");
        priceChange.DisplayName.ShouldBe("价格");
        priceChange.NewValue.ShouldBe("99.9");

        // Status 没有设置 DisplayName，应该使用属性名
        var statusChange = record.Changes.Single(c => c.PropertyName == "Status");
        statusChange.DisplayName.ShouldBe("Status");
        statusChange.NewValue.ShouldBe("Active");
    }

    [Fact]
    public async Task CaptureChanges_ModifyEntity_ShouldCaptureOnlyModifiedTrackedProperties()
    {
        // Arrange - 先插入数据
        var product = new TestProduct
        {
            Id = 1,
            Name = "原始名称",
            Price = 50m,
            Status = "Active",
            InternalRemark = "备注"
        };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        // 修改部分字段
        product.Name = "新名称";
        product.InternalRemark = "修改后的备注"; // 未标记 [AuditTracked]，不应追踪

        // Act
        var changes = AuditTrailChangeTracker.CaptureChanges(_dbContext.ChangeTracker, 1002, "李四");

        // Assert
        changes.Count.ShouldBe(1);
        var record = changes[0];
        record.ChangeType.ShouldBe("Modified");

        // 只有 Name 被修改且有 [AuditTracked]，Price 和 Status 没变
        record.Changes.Count.ShouldBe(1);
        var nameChange = record.Changes[0];
        nameChange.PropertyName.ShouldBe("Name");
        nameChange.DisplayName.ShouldBe("产品名称");
        nameChange.OriginalValue.ShouldBe("原始名称");
        nameChange.NewValue.ShouldBe("新名称");
    }

    [Fact]
    public async Task CaptureChanges_DeleteEntity_ShouldCaptureTrackedProperties()
    {
        // Arrange
        var product = new TestProduct
        {
            Id = 1,
            Name = "待删除产品",
            Price = 100m,
            Status = "Active",
            InternalRemark = "备注"
        };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        _dbContext.Products.Remove(product);

        // Act
        var changes = AuditTrailChangeTracker.CaptureChanges(_dbContext.ChangeTracker, 1003, "王五");

        // Assert
        changes.Count.ShouldBe(1);
        var record = changes[0];
        record.ChangeType.ShouldBe("Deleted");
        record.Changes.Count.ShouldBe(3); // Name, Price, Status

        var nameChange = record.Changes.Single(c => c.PropertyName == "Name");
        nameChange.OriginalValue.ShouldBe("待删除产品");
        nameChange.NewValue.ShouldBeNull();
    }

    [Fact]
    public void CaptureChanges_UntrackedEntity_ShouldReturnEmpty()
    {
        // Arrange - 添加没有 [AuditTracked] 标记的实体
        _dbContext.UntrackedEntities.Add(new TestUntrackedEntity
        {
            Id = 1, Name = "测试", Value = 100
        });

        // Act
        var changes = AuditTrailChangeTracker.CaptureChanges(_dbContext.ChangeTracker, 1001, "张三");

        // Assert
        changes.ShouldBeEmpty();
    }

    [Fact]
    public async Task CaptureChanges_ModifyOnlyUntrackedProperty_ShouldReturnEmpty()
    {
        // Arrange
        var product = new TestProduct
        {
            Id = 1,
            Name = "产品",
            Price = 50m,
            Status = "Active",
            InternalRemark = "初始备注"
        };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        // 只修改未标记的属性
        product.InternalRemark = "修改后的备注";

        // Act
        var changes = AuditTrailChangeTracker.CaptureChanges(_dbContext.ChangeTracker, 1001, "张三");

        // Assert - 实体有变更但没有被追踪的属性被修改
        changes.ShouldBeEmpty();
    }

    [Fact]
    public void CaptureChanges_NullOperator_ShouldStillCapture()
    {
        // Arrange
        _dbContext.Products.Add(new TestProduct
        {
            Id = 1,
            Name = "匿名产品",
            Price = 10m,
            Status = "Draft"
        });

        // Act - 操作人信息为空
        var changes = AuditTrailChangeTracker.CaptureChanges(_dbContext.ChangeTracker, null, null);

        // Assert
        changes.Count.ShouldBe(1);
        changes[0].OperatorId.ShouldBeNull();
        changes[0].OperatorName.ShouldBeNull();
    }

    [Fact]
    public async Task CaptureChanges_MultipleEntities_ShouldCaptureAll()
    {
        // Arrange - 多个实体同时变更
        var product1 = new TestProduct { Id = 1, Name = "产品1", Price = 10m, Status = "Active" };
        var product2 = new TestProduct { Id = 2, Name = "产品2", Price = 20m, Status = "Active" };
        _dbContext.Products.AddRange(product1, product2);
        await _dbContext.SaveChangesAsync();

        product1.Price = 15m;
        product2.Name = "产品2修改";

        // Act
        var changes = AuditTrailChangeTracker.CaptureChanges(_dbContext.ChangeTracker, 1001, "张三");

        // Assert
        changes.Count.ShouldBe(2);
    }

    [Fact]
    public void CaptureChanges_NoChanges_ShouldReturnEmpty()
    {
        // Act - 没有任何实体变更
        var changes = AuditTrailChangeTracker.CaptureChanges(_dbContext.ChangeTracker, 1001, "张三");

        // Assert
        changes.ShouldBeEmpty();
    }

    [Fact]
    public async Task CaptureChanges_ModifyMultipleTrackedProperties_ShouldCaptureAll()
    {
        // Arrange
        var product = new TestProduct
        {
            Id = 1,
            Name = "原始",
            Price = 100m,
            Status = "Draft",
            InternalRemark = "备注"
        };
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        // 同时修改多个被追踪的属性
        product.Name = "新名称";
        product.Price = 200m;
        product.Status = "Published";

        // Act
        var changes = AuditTrailChangeTracker.CaptureChanges(_dbContext.ChangeTracker, 1001, "张三");

        // Assert
        changes.Count.ShouldBe(1);
        var record = changes[0];
        record.Changes.Count.ShouldBe(3);
        record.Changes.ShouldContain(c => c.PropertyName == "Name" && c.OriginalValue == "原始" && c.NewValue == "新名称");
        record.Changes.ShouldContain(c => c.PropertyName == "Price" && c.OriginalValue == "100" && c.NewValue == "200");
        record.Changes.ShouldContain(c => c.PropertyName == "Status" && c.OriginalValue == "Draft" && c.NewValue == "Published");
    }
}

namespace BeniceSoft.Abp.Extensions.DynamicQuery.EfCore.Tests.TestModels;

/// <summary>
/// 测试实体
/// </summary>
public class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Age { get; set; }
    public long TotalCount { get; set; }
    public double Price { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid UniqueId { get; set; }
    public List<string> Tags { get; set; } = new();
    public TestNestedEntity? Nested { get; set; }
}

/// <summary>
/// 嵌套实体
/// </summary>
public class TestNestedEntity
{
    public string Code { get; set; } = string.Empty;
    public int Value { get; set; }
}

/// <summary>
/// 测试用的 DynamicQueryRequest 实现
/// </summary>
public class TestDynamicQueryRequest : BeniceSoft.Extensions.DynamicQuery.IDynamicQueryRequest
{
    public List<BeniceSoft.Extensions.DynamicQuery.DynamicQueryConditionGroup>? ConditionGroups { get; set; }
}


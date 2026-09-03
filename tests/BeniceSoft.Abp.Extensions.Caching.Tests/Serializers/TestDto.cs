namespace BeniceSoft.Abp.Extensions.Caching.Tests.Serializers;

/// <summary>
/// 测试用 DTO
/// </summary>
public class TestDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// 嵌套对象测试用 DTO
/// </summary>
public class ParentDto
{
    public int Id { get; set; }
    public TestDto? Child { get; set; }
}


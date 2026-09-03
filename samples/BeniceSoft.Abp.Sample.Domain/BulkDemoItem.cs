using Volo.Abp.Domain.Entities;

namespace BeniceSoft.Abp.Sample.Domain;

/// <summary>
/// 专供 Bulk / Sequence / Hint 演示与集成测试的简单实体（无审计、无外键）。
/// </summary>
public class BulkDemoItem : Entity<Guid>
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string BatchTag { get; set; } = string.Empty;

    /// <summary>
    /// 乐观并发令牌，用于 ForceSaveChange 演示。
    /// </summary>
    public int Version { get; set; }

    public BulkDemoItem()
    {
    }

    public BulkDemoItem(Guid id, string code, string name, int quantity, string batchTag)
    {
        Id = id;
        Code = code;
        Name = name;
        Quantity = quantity;
        BatchTag = batchTag;
        Version = 1;
    }
}

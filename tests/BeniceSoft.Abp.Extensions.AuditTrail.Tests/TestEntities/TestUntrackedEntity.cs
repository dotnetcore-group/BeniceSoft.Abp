namespace BeniceSoft.Abp.Extensions.AuditTrail.Tests.TestEntities;

/// <summary>
/// 完全没有 [AuditTracked] 标记的实体
/// </summary>
public class TestUntrackedEntity
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Value { get; set; }
}


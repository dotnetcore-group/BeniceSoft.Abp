using Volo.Abp.Domain.Entities;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests.Fixture;

/// <summary>按月分表实体。</summary>
public class ShardLedger : Entity<Guid>
{
    public string Code { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime BizMonth { get; set; }
    public string BatchTag { get; set; } = string.Empty;

    public ShardLedger()
    {
    }

    public ShardLedger(Guid id, string code, decimal amount, DateTime bizMonth, string batchTag)
    {
        Id = id;
        Code = code;
        Amount = amount;
        BizMonth = bizMonth;
        BatchTag = batchTag;
    }
}

/// <summary>取模分表实体。</summary>
public class ShardBucket : Entity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public int BucketKey { get; set; }
    public string BatchTag { get; set; } = string.Empty;

    public ShardBucket()
    {
    }

    public ShardBucket(Guid id, string name, int bucketKey, string batchTag)
    {
        Id = id;
        Name = name;
        BucketKey = bucketKey;
        BatchTag = batchTag;
    }
}

/// <summary>按 Area 分库实体（无分表）。</summary>
public class ShardAreaOrder : Entity<Guid>
{
    public string Area { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string BatchTag { get; set; } = string.Empty;

    public ShardAreaOrder()
    {
    }

    public ShardAreaOrder(Guid id, string area, string title, string batchTag)
    {
        Id = id;
        Area = area;
        Title = title;
        BatchTag = batchTag;
    }
}

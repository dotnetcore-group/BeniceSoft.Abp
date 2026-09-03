namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests.Fixture;

public sealed class ShardLedgerMonthRoute : MonthTailTableRoute<ShardLedger>
{
    protected override bool EnabledHint => true;

    public override void Configure(EntityMetadataTableBuilder<ShardLedger> builder)
    {
        builder.WithProperty(x => x.BizMonth)
            .WithSeparator("_")
            .WithAutoCreate(true);
    }

    protected override DateTime GetBeginTime() => new(2024, 1, 1);
}

public sealed class ShardBucketModRoute : ModIntTableRoute<ShardBucket>
{
    public ShardBucketModRoute() : base(length: 1, mod: 2)
    {
    }

    protected override bool EnabledHint => true;

    public override void Configure(EntityMetadataTableBuilder<ShardBucket> builder)
    {
        builder.WithProperty(x => x.BucketKey)
            .WithSeparator("_")
            .WithAutoCreate(true);
    }
}

/// <summary>Area A → ds0，Area B → ds1。</summary>
public sealed class ShardAreaOrderDataSourceRoute : DataSourceRoute<ShardAreaOrder, string>
{
    private readonly List<string> _dataSources = ["ds0", "ds1"];

    protected override bool EnabledHint => true;

    public override void Configure(EntityMetadataDataSourceBuilder<ShardAreaOrder> builder)
    {
        builder.WithProperty(x => x.Area).WithAutoCreate(true);
    }

    public override IReadOnlyList<string> GetAll() => _dataSources;

    public override bool Add(string name)
    {
        if (_dataSources.Contains(name))
        {
            return false;
        }

        _dataSources.Add(name);
        return true;
    }

    public override string GetKey(object shardingKey)
    {
        var area = shardingKey?.ToString() ?? string.Empty;
        return area.StartsWith("B", StringComparison.OrdinalIgnoreCase) ? "ds1" : "ds0";
    }
}

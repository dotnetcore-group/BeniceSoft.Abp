using BeniceSoft.Abp.EntityFrameworkCore.Sharding;
using BeniceSoft.Abp.Sample.Domain;

namespace BeniceSoft.Abp.Sample.EntityFrameworkCore.Sharding;

/// <summary>
/// 订单按月分表：sales_orders_yyyyMM（如 sales_orders_202401）
/// </summary>
public sealed class SalesOrderMonthRoute : MonthTailTableRoute<SalesOrder>
{
    protected override bool EnabledHint => true;

    public override void Configure(EntityMetadataTableBuilder<SalesOrder> builder)
    {
        builder.WithProperty(x => x.OrderTime)
            .WithSeparator("_")
            .WithAutoCreate(true);
    }

    /// <summary>历史最早可路由月份；启动补偿会从该月建到当前月。</summary>
    protected override DateTime GetBeginTime() => new(2024, 1, 1);

    //protected override string GetTail(DateTime date) => $"{date:yyyyMM}";
}

/// <summary>
/// 订单分库：按租户映射到物理库名（ds0/tenant_a/tenant_b）。
/// TenantId 为 null / Empty 时走默认库 ds0。
/// </summary>
public sealed class SalesOrderTenantDataSourceRoute : DataSourceRoute<SalesOrder, Guid?>
{
    private static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly List<string> _dataSources = ["ds0", "tenant_a", "tenant_b"];

    protected override bool EnabledHint => true;

    public override void Configure(EntityMetadataDataSourceBuilder<SalesOrder> builder)
        => builder.WithProperty(x => x.TenantId).WithAutoCreate(true);

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
        // null / Empty / 非 Guid → 默认库；boxed Guid? 非空会以 Guid 形式传入
        var id = shardingKey is Guid g ? g : Guid.Empty;
        if (id == Guid.Empty)
        {
            return "ds0";
        }

        if (id == TenantAId)
        {
            return "tenant_a";
        }

        if (id == TenantBId)
        {
            return "tenant_b";
        }

        return "ds0";
    }
}
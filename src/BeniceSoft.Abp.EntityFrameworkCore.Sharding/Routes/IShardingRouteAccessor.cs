namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IShardingRouteAccessor
{
    ShardingRouteContext? Context { get; set; }
}

public class ShardingRouteAccessor : IShardingRouteAccessor
{
    private static readonly AsyncLocal<ShardingRouteContext?> _local = new();

    /// <summary>
    /// sharding route context use in using code block
    /// </summary>
    public ShardingRouteContext? Context
    {
        get => _local.Value;
        set => _local.Value = value;
    }
}

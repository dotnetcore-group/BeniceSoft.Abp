namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IShardingRouteManager
{
    ShardingRouteContext? Current { get; }

    /// <summary>
    /// 创建路由scope
    /// </summary>
    /// <returns></returns>
    ShardingRouteScope CreateScope();
}

public class ShardingRouteManager(IShardingRouteAccessor accessor) : IShardingRouteManager
{
    public ShardingRouteContext? Current => accessor.Context;

    public ShardingRouteScope CreateScope()
    {
        var previous = accessor.Context;
        accessor.Context = new();
        return new ShardingRouteScope(accessor, previous);
    }
}

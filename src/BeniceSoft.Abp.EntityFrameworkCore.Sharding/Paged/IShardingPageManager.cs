namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IShardingPageManager
{
    ShardingPageContext? Current { get; }

    /// <summary>
    /// 创建分页scope
    /// </summary>
    /// <returns></returns>
    ShardingPageScope CreateScope();
}

internal sealed class ShardingPageManager(IShardingPageAccessor accessor) : IShardingPageManager
{
    public ShardingPageContext? Current => accessor.Context;

    public ShardingPageScope CreateScope()
    {
        var previous = accessor.Context;
        accessor.Context = new();
        return new ShardingPageScope(accessor, previous);
    }
}

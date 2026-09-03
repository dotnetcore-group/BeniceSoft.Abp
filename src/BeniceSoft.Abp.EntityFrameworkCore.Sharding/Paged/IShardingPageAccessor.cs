namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IShardingPageAccessor
{
    ShardingPageContext? Context { get; set; }
}

internal sealed class ShardingPageAccessor : IShardingPageAccessor
{
    private static readonly AsyncLocal<ShardingPageContext?> _local = new();

    public ShardingPageContext? Context
    {
        get => _local.Value;
        set => _local.Value = value;
    }
}

/// <summary>
/// 构造函数
/// </summary>
/// <param name="accessor"></param>
/// <param name="previous"></param>
public sealed class ShardingPageScope(IShardingPageAccessor accessor, ShardingPageContext? previous) : IDisposable
{

    /// <summary>
    /// 分表配置访问器
    /// </summary>
    public IShardingPageAccessor Accessor { get; } = accessor;

    /// <summary>
    /// 回收
    /// </summary>
    public void Dispose()
    {
        Accessor.Context = previous;
        GC.SuppressFinalize(this);
    }
}

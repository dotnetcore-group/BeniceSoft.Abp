namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// 分布式锁摘要信息
/// </summary>
public readonly struct RedisLockSummary
{
    /// <summary>
    /// 成功获取锁的实例数量
    /// </summary>
    public int Acquired { get; }

    /// <summary>
    /// 冲突的实例数量（锁被其他客户端持有）
    /// </summary>
    public int Conflicted { get; }

    /// <summary>
    /// 发生错误的实例数量
    /// </summary>
    public int Error { get; }

    /// <summary>
    /// 初始化锁摘要
    /// </summary>
    public RedisLockSummary(int acquired, int conflicted, int error)
    {
        Acquired = acquired;
        Conflicted = conflicted;
        Error = error;
    }

    /// <summary>
    /// 空摘要
    /// </summary>
    public static RedisLockSummary Empty => new(0, 0, 0);

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Acquired: {Acquired}, Conflicted: {Conflicted}, Error: {Error}";
    }
}
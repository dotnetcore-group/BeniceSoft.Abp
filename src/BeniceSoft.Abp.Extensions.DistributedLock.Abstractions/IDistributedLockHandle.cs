namespace BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;

/// <summary>
/// 本次获取的分布式锁句柄，释放时仅操作本次持有的锁
/// </summary>
public interface IDistributedLockHandle : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 锁定的资源名称
    /// </summary>
    string ResourceId { get; }

    /// <summary>
    /// 本次锁的唯一标识
    /// </summary>
    string LockId { get; }

    /// <summary>
    /// 本地记录的锁状态，不能代替 Redis 所有权检查
    /// </summary>
    bool IsAcquired { get; }

    /// <summary>
    /// 使用本次锁的唯一标识手动续期，句柄已释放或续期失败时返回 false
    /// </summary>
    /// <param name="expires">续期后的有效期</param>
    Task<bool> RenewAsync(TimeSpan expires);
}

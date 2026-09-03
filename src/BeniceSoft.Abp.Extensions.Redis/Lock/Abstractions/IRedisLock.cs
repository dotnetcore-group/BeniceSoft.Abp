namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// Redis 分布式锁接口
/// </summary>
public interface IRedisLock : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 锁定的资源名称
    /// </summary>
    string Resource { get; }

    /// <summary>
    /// 锁的唯一标识
    /// </summary>
    string LockId { get; }

    /// <summary>
    /// 是否成功获取锁
    /// </summary>
    bool IsAcquired { get; }

    /// <summary>
    /// 锁的状态
    /// </summary>
    RedisLockStatus Status { get; }

    /// <summary>
    /// 获取锁的实例的详细信息
    /// </summary>
    RedisLockSummary InstanceSummary { get; }

    /// <summary>
    /// 扩展锁的次数
    /// </summary>
    int ExtendCount { get; }

    /// <summary>
    /// 释放锁（同步）
    /// </summary>
    /// <returns>是否成功释放</returns>
    bool Unlock();

    /// <summary>
    /// 释放锁（异步）
    /// </summary>
    /// <returns>是否成功释放</returns>
    Task<bool> UnlockAsync();

    /// <summary>
    /// 续期锁（同步）
    /// </summary>
    /// <param name="expiryTime">新的过期时间</param>
    /// <returns>续期结果</returns>
    RedisLockResult Extend(TimeSpan? expiryTime = null);

    /// <summary>
    /// 续期锁（异步）
    /// </summary>
    /// <param name="expiryTime">新的过期时间</param>
    /// <returns>续期结果</returns>
    Task<RedisLockResult> ExtendAsync(TimeSpan? expiryTime = null);
}

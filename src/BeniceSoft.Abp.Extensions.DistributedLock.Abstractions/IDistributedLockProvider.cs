namespace BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;

public interface IDistributedLockProvider : IDisposable
{
    /// <summary>
    /// 分配锁
    /// </summary>
    /// <param name="resourceId">资源id</param>
    /// <param name="expires">过期时间</param>
    /// <param name="wait">等待时间</param>
    /// <param name="interval">间隔时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<bool> AcquireAsync(string resourceId, TimeSpan expires, TimeSpan wait, TimeSpan interval,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 分配锁（支持自动续期）
    /// </summary>
    /// <param name="resourceId">资源id</param>
    /// <param name="expires">过期时间</param>
    /// <param name="wait">等待时间</param>
    /// <param name="interval">间隔时间</param>
    /// <param name="autoRenew">是否自动续期</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<bool> AcquireAsync(string resourceId, TimeSpan expires, TimeSpan wait, TimeSpan interval,
         bool autoRenew, CancellationToken cancellationToken = default);

    /// <summary>
    /// 尝试分配锁
    /// </summary>
    /// <param name="resourceId">资源id</param>
    /// <param name="expires">过期时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<bool> TryAcquireAsync(string resourceId, TimeSpan expires,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 尝试分配锁（支持自动续期）
    /// </summary>
    /// <param name="resourceId">资源id</param>
    /// <param name="expires">过期时间</param>
    /// <param name="autoRenew">是否自动续期</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    Task<bool> TryAcquireAsync(string resourceId, TimeSpan expires,
        bool autoRenew, CancellationToken cancellationToken = default);

    /// <summary>
    /// 释放锁
    /// </summary>
    /// <param name="resourceId"></param>
    /// <returns></returns>
    Task ReleaseLockAsync(string resourceId);

    /// <summary>
    /// 手动续期锁
    /// </summary>
    /// <param name="resourceId">资源id</param>
    /// <param name="extends">延长时间</param>
    /// <returns>是否续期成功</returns>
    Task<bool> RenewLockAsync(string resourceId, TimeSpan extends);
}
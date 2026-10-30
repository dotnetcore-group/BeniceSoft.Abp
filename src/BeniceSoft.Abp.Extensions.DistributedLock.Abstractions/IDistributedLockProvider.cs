namespace BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;

public interface IDistributedLockProvider : IDisposable
{
    /// <summary>
    /// 尝试获取分布式锁
    /// </summary>
    /// <param name="resourceId">资源名称</param>
    /// <param name="expires">锁的有效期</param>
    /// <param name="wait">获取失败后的等待窗口，为零时只尝试一轮</param>
    /// <param name="interval">重试获取锁间隔，需要等待时必须大于零</param>
    /// <param name="autoRenew">是否自动续期至锁释放</param>
    /// <param name="cancellationToken">取消令牌，取消时抛出 OperationCanceledException</param>
    /// <returns>本次获取的锁，等待耗尽仍未获取到锁时返回 null</returns>
    Task<IDistributedLockHandle?> TryAcquireAsync(string resourceId, TimeSpan expires, TimeSpan wait,
        TimeSpan interval, bool autoRenew = false, CancellationToken cancellationToken = default);
}

namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// 分布式锁配置
/// </summary>
public class RedisLockProfile
{
    /// <summary>
    /// 分布式锁关键标识
    /// </summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>
    /// 获取锁的等待窗口，为零时只尝试一轮，网络调用和清理可能超过此时间
    /// </summary>
    public TimeSpan WaitTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// 定义任务执行最大超时时间（即任务锁最多保留的时间，默认值10s）。
    /// 默认情况下任务锁被获取，任务执行完毕立即释放。
    /// 但是若任务执行期间异常等情况，任务锁未被释放并被一直保留在分布式内存中。
    /// 另外任务锁保留期间，任何对锁的获取都会超时失败。
    /// WaitTime >= ExpiryTime，这样就能保证锁一定能被获取到，无非等待时间较长，具体看业务情况而定。
    /// </summary>
    public TimeSpan ExpiryTime { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 重试获取锁时间间隔(ms)，默认为50ms
    /// </summary>
    public int RetryInterval { get; set; } = 50;

    /// <summary>
    /// 检测进程是否保持激活（自动续期）
    /// </summary>
    public bool KeepLive { get; set; } = true;

    /// <summary>
    /// value值标识，当标识不为空的时候，redis服务器原有的标识和其标识一致，将强制获取锁。
    /// </summary>
    public string? LockId { get; set; }

    /// <summary>
    /// 锁的 key 格式，默认为 "lock:{0}"
    /// </summary>
    public string KeyFormat { get; set; } = "lock:{0}";
}

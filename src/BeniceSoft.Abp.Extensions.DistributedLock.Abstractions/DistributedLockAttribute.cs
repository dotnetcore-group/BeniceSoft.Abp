namespace BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;

/// <summary>
/// 分布式锁
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class DistributedLockAttribute : Attribute
{
    /// <summary>
    /// 资源id
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// 过期毫秒数 默认1min
    /// </summary>
    public int ExpiresMilliseconds { get; set; } = 60000;

    /// <summary>
    /// 获取锁失败，等待毫秒数 默认100ms
    /// </summary>
    public int WaitMilliseconds { get; set; } = 100;

    /// <summary>
    /// 获取锁失败，重试获取间隔时间 默认25ms
    /// </summary>
    public int IntervalMilliseconds { get; set; } = 25;

    /// <summary>
    /// 是否启用自动续期（适用于长时任务）
    /// 默认关闭，开启后会在 1/2 处自动重置 ExpiresMilliseconds
    /// </summary>
    public bool AutoRenew { get; set; } = false;
}
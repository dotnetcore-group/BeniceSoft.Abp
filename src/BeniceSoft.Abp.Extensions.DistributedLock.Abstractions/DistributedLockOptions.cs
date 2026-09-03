namespace BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;

/// <summary>
/// 分布式锁 Redis 配置
/// </summary>
public class DistributedLockOptions
{
    /// <summary>
    /// 分布式锁专用 Redis 连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}

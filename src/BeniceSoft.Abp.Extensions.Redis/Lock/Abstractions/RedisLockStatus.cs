namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// 分布式锁状态
/// </summary>
public enum RedisLockStatus
{
    /// <summary>
    /// 尚未成功获取或释放锁
    /// </summary>
    Unlocked = 0,

    /// <summary>
    /// 已成功获取锁
    /// </summary>
    Acquired = 1,

    /// <summary>
    /// 未获取锁，因为没有资源可用（未达到法定数量）
    /// </summary>
    NoQuorum = 2,

    /// <summary>
    /// 未获取该锁，因为它当前被另一个 LockId 锁定
    /// </summary>
    Conflicted = 3,

    /// <summary>
    /// 已过期
    /// </summary>
    Expired = 4
}
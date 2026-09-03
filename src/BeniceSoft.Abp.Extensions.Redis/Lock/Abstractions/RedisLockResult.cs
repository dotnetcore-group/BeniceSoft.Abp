namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// 分布式锁操作结果
/// </summary>
public enum RedisLockResult
{
    /// <summary>
    /// 操作成功
    /// </summary>
    Success = 1,

    /// <summary>
    /// 冲突（锁被其他客户端持有）
    /// </summary>
    Conflicted = -1,

    /// <summary>
    /// 错误
    /// </summary>
    Error = 0
}
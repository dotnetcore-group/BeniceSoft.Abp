namespace BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;

/// <summary>
/// 限流结果
/// </summary>
public class RateLimitResult
{
    /// <summary>
    /// 是否允许请求
    /// </summary>
    public bool IsAllowed { get; private set; }

    /// <summary>
    /// 剩余可用配额
    /// </summary>
    public long Remaining { get; private set; }

    /// <summary>
    /// 限制总量
    /// </summary>
    public long Limit { get; private set; }

    /// <summary>
    /// 需要等待的秒数（被拒绝时）
    /// </summary>
    public int RetryAfterSeconds { get; private set; }

    /// <summary>
    /// 创建允许结果
    /// </summary>
    public static RateLimitResult Allowed(long remaining, long limit)
    {
        return new RateLimitResult
        {
            IsAllowed = true,
            Remaining = remaining,
            Limit = limit,
            RetryAfterSeconds = 0
        };
    }

    /// <summary>
    /// 创建拒绝结果
    /// </summary>
    public static RateLimitResult Rejected(int retryAfterSeconds, long limit)
    {
        return new RateLimitResult
        {
            IsAllowed = false,
            Remaining = 0,
            Limit = limit,
            RetryAfterSeconds = retryAfterSeconds
        };
    }
}


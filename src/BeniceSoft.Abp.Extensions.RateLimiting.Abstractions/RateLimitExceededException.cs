namespace BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;

/// <summary>
/// 超出速率限制异常
/// </summary>
public class RateLimitExceededException : Exception
{
    /// <summary>
    /// 限流 Key
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 剩余配额
    /// </summary>
    public long Remaining { get; }

    /// <summary>
    /// 限制总量
    /// </summary>
    public long Limit { get; }

    /// <summary>
    /// 建议的重试等待时间（秒）
    /// </summary>
    public int RetryAfterSeconds { get; }

    public RateLimitExceededException(string key, long limit, int retryAfterSeconds, string? message = null)
        : base(message ?? $"请求过于频繁，请 {retryAfterSeconds} 秒后再试")
    {
        Key = key;
        Remaining = 0;
        Limit = limit;
        RetryAfterSeconds = retryAfterSeconds;
    }
}


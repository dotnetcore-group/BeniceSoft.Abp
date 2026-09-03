namespace BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;

/// <summary>
/// 速率限流器接口
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// 尝试获取许可
    /// </summary>
    /// <param name="key">限流 Key</param>
    /// <param name="permitLimit">时间窗口内允许的请求数</param>
    /// <param name="windowSeconds">时间窗口（秒）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>限流结果</returns>
    Task<RateLimitResult> TryAcquireAsync(
        string key,
        int permitLimit,
        int windowSeconds,
        CancellationToken cancellationToken = default);
}


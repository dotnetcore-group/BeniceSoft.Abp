namespace BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;

/// <summary>
/// 速率限流标签
/// <para>
/// 用于标记需要限流的方法，支持按 IP/用户/租户/自定义 Key 进行限流
/// </para>
/// </summary>
/// <example>
/// <code>
/// // 每个用户每分钟最多 5 次请求
/// [RateLimit(LimitBy = RateLimitBy.UserId, PermitLimit = 5, WindowSeconds = 60)]
/// public async Task&lt;bool&gt; SendSmsCodeAsync(string phone) { }
///
/// // 按 IP 限流（默认）
/// [RateLimit(PermitLimit = 10, WindowSeconds = 60)]
/// public async Task&lt;Result&gt; QueryAsync() { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public class RateLimitAttribute : Attribute
{
    /// <summary>
    /// 限流维度，默认按 IP 限流
    /// </summary>
    public RateLimitBy LimitBy { get; set; } = RateLimitBy.Ip;

    /// <summary>
    /// 自定义限流 Key（仅当 LimitBy = Custom 时有效）
    /// <para>
    /// 支持 SmartFormat 参数插值，例如："{phone}"
    /// </para>
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// 时间窗口内允许的最大请求数
    /// </summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>
    /// 时间窗口（秒）
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// 超出限流时是否抛出异常，默认 true
    /// <para>
    /// 如果为 false，方法将正常执行但会记录日志
    /// </para>
    /// </summary>
    public bool ThrowOnExceeded { get; set; } = true;

    /// <summary>
    /// 超出限流时的提示消息
    /// </summary>
    public string? Message { get; set; }
}


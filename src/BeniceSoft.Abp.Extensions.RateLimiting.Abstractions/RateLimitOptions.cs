namespace BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;

/// <summary>
/// 速率限流配置选项
/// </summary>
public class RateLimitOptions
{
    /// <summary>
    /// Redis Key 前缀
    /// </summary>
    public string KeyPrefix { get; set; } = "ratelimit";

    /// <summary>
    /// 默认的超限提示消息
    /// </summary>
    public string DefaultMessage { get; set; } = "请求过于频繁，请稍后再试";

    /// <summary>
    /// 是否启用限流，默认启用
    /// </summary>
    public bool Enabled { get; set; } = true;
}


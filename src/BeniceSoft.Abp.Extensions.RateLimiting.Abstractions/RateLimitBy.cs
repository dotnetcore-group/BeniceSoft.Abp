namespace BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;

/// <summary>
/// 限流维度
/// </summary>
public enum RateLimitBy
{
    /// <summary>
    /// 按客户端 IP 限流
    /// </summary>
    Ip,

    /// <summary>
    /// 按用户 ID 限流
    /// </summary>
    UserId,

    /// <summary>
    /// 按租户 ID 限流
    /// </summary>
    TenantId,

    /// <summary>
    /// 自定义 Key（通过 Key 属性指定）
    /// </summary>
    Custom,

    /// <summary>
    /// 全局限流（所有请求共享配额）
    /// </summary>
    Global
}


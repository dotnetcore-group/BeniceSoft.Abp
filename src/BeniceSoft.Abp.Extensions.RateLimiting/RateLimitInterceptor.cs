using BeniceSoft.Abp.Core.Users;
using BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartFormat;
using System.Reflection;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DynamicProxy;

namespace BeniceSoft.Abp.Extensions.RateLimiting;

/// <summary>
/// 速率限流拦截器
/// </summary>
public class RateLimitInterceptor : AbpInterceptor, ITransientDependency
{
    private readonly ILogger<RateLimitInterceptor> _logger;
    private readonly IRateLimiter _rateLimiter;
    private readonly IBeniceSoftCurrentUser _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RateLimitOptions _options;

    public RateLimitInterceptor(
        ILogger<RateLimitInterceptor> logger,
        IRateLimiter rateLimiter,
        IBeniceSoftCurrentUser currentUser,
        IHttpContextAccessor httpContextAccessor,
        IOptions<RateLimitOptions> options)
    {
        _logger = logger;
        _rateLimiter = rateLimiter;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public override async Task InterceptAsync(IAbpMethodInvocation invocation)
    {
        var rateLimitAttribute = invocation.Method.GetCustomAttribute<RateLimitAttribute>();
        if (rateLimitAttribute is null)
        {
            await invocation.ProceedAsync();
            return;
        }

        if (!_options.Enabled)
        {
            await invocation.ProceedAsync();
            return;
        }

        var limitKey = GetLimitKey(rateLimitAttribute, invocation);

        var result = await _rateLimiter.TryAcquireAsync(
            limitKey,
            rateLimitAttribute.PermitLimit,
            rateLimitAttribute.WindowSeconds);

        if (!result.IsAllowed)
        {
            _logger.LogWarning("请求被限流: Key={Key}, Limit={Limit}, RetryAfter={RetryAfter}s",
                limitKey, result.Limit, result.RetryAfterSeconds);

            if (rateLimitAttribute.ThrowOnExceeded)
            {
                var message = rateLimitAttribute.Message ?? _options.DefaultMessage;
                throw new RateLimitExceededException(
                    limitKey,
                    result.Limit,
                    result.RetryAfterSeconds,
                    message);
            }
        }

        await invocation.ProceedAsync();
    }

    /// <summary>
    /// 生成限流 Key
    /// </summary>
    private string GetLimitKey(RateLimitAttribute attribute, IAbpMethodInvocation invocation)
    {
        var methodInfo = invocation.Method;
        var methodKey = $"{methodInfo.DeclaringType?.FullName}.{methodInfo.Name}";

        // 获取维度标识
        var dimensionKey = attribute.LimitBy switch
        {
            RateLimitBy.Ip => $"ip:{GetClientIp()}",
            RateLimitBy.UserId => $"user:{GetUserId()}",
            RateLimitBy.TenantId => $"tenant:{GetTenantId()}",
            RateLimitBy.Global => "global",
            RateLimitBy.Custom => GetCustomKey(attribute.Key, invocation),
            _ => $"ip:{GetClientIp()}"
        };

        return $"{methodKey}:{dimensionKey}";
    }

    /// <summary>
    /// 获取自定义 Key
    /// </summary>
    private string GetCustomKey(string? keyTemplate, IAbpMethodInvocation invocation)
    {
        if (string.IsNullOrWhiteSpace(keyTemplate))
        {
            return $"ip:{GetClientIp()}";
        }

        return Smart.Format(keyTemplate, invocation.ArgumentsDictionary);
    }

    /// <summary>
    /// 获取客户端 IP
    /// </summary>
    private string GetClientIp()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null) return "unknown";

        // 优先从代理转发头获取
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            return realIp;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// 获取用户 ID
    /// </summary>
    private string GetUserId()
    {
        return _currentUser.Id?.ToString() ?? "anonymous";
    }

    /// <summary>
    /// 获取租户 ID
    /// </summary>
    private string GetTenantId()
    {
        return _currentUser.TenantId?.ToString() ?? "default";
    }
}


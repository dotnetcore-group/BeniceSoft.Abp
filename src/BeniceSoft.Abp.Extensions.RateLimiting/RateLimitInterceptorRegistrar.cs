using BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DynamicProxy;

namespace BeniceSoft.Abp.Extensions.RateLimiting;

/// <summary>
/// 速率限流拦截器注册器
/// </summary>
public class RateLimitInterceptorRegistrar
{
    public static void RegisterIfNeeded(IOnServiceRegistredContext context)
    {
        if (ShouldIntercept(context.ImplementationType))
        {
            context.Interceptors.TryAdd<RateLimitInterceptor>();
        }
    }

    private static bool ShouldIntercept(Type type)
    {
        // 是否是动态代理忽略的类型
        if (DynamicProxyIgnoreTypes.Contains(type))
        {
            return false;
        }

        // 方法有 RateLimitAttribute 标签
        if (type.GetMethods().Any(m => m.IsDefined(typeof(RateLimitAttribute), true)))
        {
            return true;
        }

        return false;
    }
}


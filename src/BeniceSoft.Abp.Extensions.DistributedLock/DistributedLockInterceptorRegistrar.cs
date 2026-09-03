using BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DynamicProxy;

namespace BeniceSoft.Abp.Extensions.DistributedLock;

public class DistributedLockInterceptorRegistrar
{
    public static void RegisterIfNeeded(IOnServiceRegistredContext context)
    {
        if (ShouldIntercept(context.ImplementationType))
        {
            context.Interceptors.TryAdd<DistributedLockInterceptor>();
        }
    }

    private static bool ShouldIntercept(Type type)
    {
        if (DynamicProxyIgnoreTypes.Contains(type))
        {
            return false;
        }

        if (type.GetMethods().Any(m => m.IsDefined(typeof(DistributedLockAttribute), true)))
        {
            return true;
        }

        return false;
    }
}
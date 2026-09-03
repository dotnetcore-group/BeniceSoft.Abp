using System.Reflection;
using BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;
using Microsoft.Extensions.Logging;
using SmartFormat;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DynamicProxy;

namespace BeniceSoft.Abp.Extensions.DistributedLock;

public class DistributedLockInterceptor : AbpInterceptor, ITransientDependency
{
    private readonly ILogger<DistributedLockInterceptor> _logger;
    private readonly IDistributedLockProvider _distributedLockProvider;

    public DistributedLockInterceptor(
        ILogger<DistributedLockInterceptor> logger,
        IDistributedLockProvider distributedLockProvider)
    {
        _logger = logger;
        _distributedLockProvider = distributedLockProvider;
    }

    public override async Task InterceptAsync(IAbpMethodInvocation invocation)
    {
        var distributedLockAttribute = invocation.Method.GetCustomAttribute<DistributedLockAttribute>(true);
        if (distributedLockAttribute is null)
        {
            await invocation.ProceedAsync();
            return;
        }

        var resourceId = GetResourceId(distributedLockAttribute, invocation);
        var acquired = await _distributedLockProvider.AcquireAsync(
            resourceId,
            TimeSpan.FromMilliseconds(distributedLockAttribute.ExpiresMilliseconds),
            TimeSpan.FromMilliseconds(distributedLockAttribute.WaitMilliseconds),
            TimeSpan.FromMilliseconds(distributedLockAttribute.IntervalMilliseconds),
            autoRenew: distributedLockAttribute.AutoRenew);

        if (!acquired)
        {
            throw new InvalidOperationException($"无法获取分布式锁: {resourceId}");
        }

        try
        {
            await invocation.ProceedAsync();
        }
        finally
        {
            await _distributedLockProvider.ReleaseLockAsync(resourceId);
        }
    }

    private static string GetResourceId(DistributedLockAttribute attribute, IAbpMethodInvocation invocation)
    {
        if (string.IsNullOrWhiteSpace(attribute.ResourceId))
        {
            var methodInfo = invocation.Method;
            return $"{methodInfo.DeclaringType?.Namespace}.{methodInfo.DeclaringType?.Name}.{methodInfo.Name}";
        }

        return Smart.Format(attribute.ResourceId, invocation.ArgumentsDictionary);
    }
}
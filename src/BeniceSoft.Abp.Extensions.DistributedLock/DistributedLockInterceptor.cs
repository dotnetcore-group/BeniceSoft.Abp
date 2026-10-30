using BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;
using Microsoft.Extensions.Logging;
using SmartFormat;
using System.Reflection;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DynamicProxy;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.Extensions.DistributedLock;

public class DistributedLockInterceptor : AbpInterceptor, ITransientDependency
{
    private readonly ILogger<DistributedLockInterceptor> _logger;
    private readonly IDistributedLockProvider _distributedLockProvider;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public DistributedLockInterceptor(
        ILogger<DistributedLockInterceptor> logger,
        IDistributedLockProvider distributedLockProvider,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _logger = logger;
        _distributedLockProvider = distributedLockProvider;
        _unitOfWorkManager = unitOfWorkManager;
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
        var unitOfWork = _unitOfWorkManager.Current;
        var lockKey = $"{typeof(DistributedLockInterceptor).FullName}:{resourceId}";

        // 同一工作单元内复用已持有的锁，避免连续或嵌套调用等待自身释放
        if (unitOfWork != null && unitOfWork.Items.TryGetValue(lockKey, out var value) &&
            value is IDistributedLockHandle existingHandle)
        {
            if (!existingHandle.IsAcquired)
            {
                throw new InvalidOperationException($"工作单元持有的分布式锁已失效: {resourceId}");
            }

            await invocation.ProceedAsync();
            return;
        }

        var handle = await _distributedLockProvider.TryAcquireAsync(
            resourceId,
            TimeSpan.FromMilliseconds(distributedLockAttribute.ExpiresMilliseconds),
            TimeSpan.FromMilliseconds(distributedLockAttribute.WaitMilliseconds),
            TimeSpan.FromMilliseconds(distributedLockAttribute.IntervalMilliseconds),
            autoRenew: distributedLockAttribute.AutoRenew);

        if (handle == null)
        {
            throw new InvalidOperationException($"无法获取分布式锁: {resourceId}");
        }

        var managedByUnitOfWork = false;
        try
        {
            if (unitOfWork != null)
            {
                // 成功提交后异步释放，失败或完成回调中断时在事务资源释放后同步清理
                unitOfWork.Disposed += (_, _) => handle.Dispose();
                unitOfWork.OnCompleted(() => handle.DisposeAsync().AsTask());
                unitOfWork.Items.Add(lockKey, handle);
                managedByUnitOfWork = true;

                _logger.LogDebug("分布式锁 {ResourceId} 已关联工作单元 {UnitOfWorkId}", resourceId, unitOfWork.Id);
            }

            await invocation.ProceedAsync();
        }
        finally
        {
            if (!managedByUnitOfWork)
            {
                await handle.DisposeAsync();
            }
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

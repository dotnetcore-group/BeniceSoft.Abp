using System.Collections.Concurrent;
using BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;
using BeniceSoft.Abp.Extensions.Redis;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Extensions.DistributedLock;

/// <summary>
/// 基于 BeniceSoft.Abp.Extensions.Redis 的分布式锁提供者
/// </summary>
public class BeniceSoftDistributedLockProvider : IDistributedLockProvider, ISingletonDependency
{
    private readonly ConcurrentDictionary<string, IRedisLock> _managedLocks = new();
    private readonly IEnumerable<RedisClient> _redisClients;
    private readonly ILogger<BeniceSoftDistributedLockProvider> _logger;
    private readonly ILogger<RedisLock>? _redisLockLogger;
    private bool _disposed;

    public BeniceSoftDistributedLockProvider(
        IEnumerable<RedisClient> redisClients,
        ILogger<BeniceSoftDistributedLockProvider> logger,
        ILogger<RedisLock>? redisLockLogger = null)
    {
        _redisClients = redisClients ?? throw new ArgumentNullException(nameof(redisClients));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _redisLockLogger = redisLockLogger;
    }

    public virtual Task<bool> TryAcquireAsync(
        string resourceId, TimeSpan expires,
        CancellationToken cancellationToken = default)
        => TryAcquireAsync(resourceId, expires, false, cancellationToken);

    public virtual async Task<bool> TryAcquireAsync(string resourceId, TimeSpan expires, bool autoRenew, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var profile = new RedisLockProfile
            {
                Resource = resourceId,
                ExpiryTime = expires,
                WaitTime = TimeSpan.Zero,
                KeepLive = autoRenew
            };

            var redisLock = await RedisLock.CreateAsync(
                _redisClients, profile, _redisLockLogger, cancellationToken);

            if (redisLock.IsAcquired)
            {
                _managedLocks.AddOrUpdate(resourceId, redisLock, (_, oldLock) =>
                {
                    oldLock.Dispose();
                    _logger.LogWarning("资源 {ResourceId} 的旧锁被新锁替换", resourceId);
                    return redisLock;
                });

                _logger.LogDebug("成功获取分布式锁: {ResourceId}, 过期: {Expires}, 自动续期: {AutoRenew}",
                    resourceId, expires, autoRenew);

                return true;
            }

            redisLock.Dispose();
            _logger.LogDebug("获取分布式锁失败: {ResourceId}, 状态: {Status}", resourceId, redisLock.Status);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分布式锁时发生异常: {ResourceId}", resourceId);
            return false;
        }
    }

    public virtual Task<bool> AcquireAsync(
        string resourceId,
        TimeSpan expires,
        TimeSpan wait,
        TimeSpan interval,
        CancellationToken cancellationToken = default)
        => AcquireAsync(resourceId, expires, wait, interval, false, cancellationToken);

    public virtual async Task<bool> AcquireAsync(
        string resourceId,
        TimeSpan expires,
        TimeSpan wait,
        TimeSpan interval,
        bool autoRenew,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var waitMs = (long)wait.TotalMilliseconds;
        var startTime = Environment.TickCount64;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await TryAcquireAsync(resourceId, expires, autoRenew, cancellationToken))
                return true;

            try { await Task.Delay(interval, cancellationToken); }
            catch (OperationCanceledException) { throw; }

        } while (Environment.TickCount64 - startTime <= waitMs);

        _logger.LogWarning("获取分布式锁超时: {ResourceId}, 等待: {Wait}", resourceId, wait);

        return false;
    }

    public Task ReleaseLockAsync(string resourceId)
    {
        if (_disposed)
        {
            _logger.LogWarning("尝试在已释放的提供者上释放锁: {ResourceId}", resourceId);
            return Task.CompletedTask;
        }

        if (_managedLocks.TryRemove(resourceId, out var redisLock))
        {
            redisLock.Dispose();
            _logger.LogDebug("成功释放分布式锁: {ResourceId}", resourceId);
        }
        else
        {
            _logger.LogWarning("尝试释放不存在的锁: {ResourceId}", resourceId);
        }

        return Task.CompletedTask;
    }

    public async Task<bool> RenewLockAsync(string resourceId, TimeSpan extends)
    {
        if (_disposed || !_managedLocks.TryGetValue(resourceId, out var redisLock))
        {
            _logger.LogWarning("尝试续期不存在的锁: {ResourceId}", resourceId);
            return false;
        }

        try
        {
            var result = await redisLock.ExtendAsync(extends);

            if (result == RedisLockResult.Success)
            {
                _logger.LogDebug("成功续期分布式锁: {ResourceId}, 延长: {Extends}", resourceId, extends);
                return true;
            }

            RemoveManagedLock(resourceId);
            _logger.LogWarning("续期失败，锁已丢失: {ResourceId}", resourceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "续期分布式锁失败: {ResourceId}", resourceId);
            RemoveManagedLock(resourceId);
        }

        return false;
    }

    private void RemoveManagedLock(string resourceId)
    {
        if (_managedLocks.TryRemove(resourceId, out var redisLock))
        {
            redisLock.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var kvp in _managedLocks)
        {
            try
            {
                kvp.Value.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "释放锁时发生异常: {ResourceId}", kvp.Key);
            }
        }

        _managedLocks.Clear();
    }
}

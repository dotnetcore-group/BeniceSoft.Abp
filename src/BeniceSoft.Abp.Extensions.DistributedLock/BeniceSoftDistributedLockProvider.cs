using BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;
using BeniceSoft.Abp.Extensions.Redis;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Extensions.DistributedLock;

/// <summary>
/// 基于 Redis 的分布式锁提供者，统一管理获取重试和独立锁句柄
/// </summary>
public class BeniceSoftDistributedLockProvider : IDistributedLockProvider, ISingletonDependency
{
    private readonly ConcurrentDictionary<string, DistributedLockHandle> _lockHandles = new();
    private readonly object _lifecycleLock = new();
    private readonly RedisClient[] _redisClients;
    private readonly ILogger<BeniceSoftDistributedLockProvider> _logger;
    private readonly ILogger<RedisLock>? _redisLockLogger;
    private volatile bool _disposed;

    public BeniceSoftDistributedLockProvider(
        IEnumerable<RedisClient> redisClients,
        ILogger<BeniceSoftDistributedLockProvider> logger,
        ILogger<RedisLock>? redisLockLogger = null)
    {
        ArgumentNullException.ThrowIfNull(redisClients);
        _redisClients = redisClients.ToArray();
        if (_redisClients.Length == 0)
        {
            throw new ArgumentException("至少需要一个 Redis 客户端", nameof(redisClients));
        }

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _redisLockLogger = redisLockLogger;
    }

    public virtual async Task<IDistributedLockHandle?> TryAcquireAsync(
        string resourceId,
        TimeSpan expires,
        TimeSpan wait,
        TimeSpan interval,
        bool autoRenew = false,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expires, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(wait, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(interval, TimeSpan.Zero);
        if (wait > TimeSpan.Zero && interval == TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "需要等待时重试间隔必须大于零");
        }

        var start = Stopwatch.GetTimestamp();
        do
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            var redisLock = await RedisLock.CreateAsync(_redisClients, new RedisLockProfile
            {
                Resource = resourceId,
                ExpiryTime = expires,
                WaitTime = TimeSpan.Zero,
                KeepLive = autoRenew
            }, _redisLockLogger, cancellationToken);

            var transferred = false;
            try
            {
                // Redis 请求在途时发生取消，也必须先清理本轮可能取得的锁
                cancellationToken.ThrowIfCancellationRequested();
                if (redisLock.IsAcquired)
                {
                    var handle = new DistributedLockHandle(redisLock,
                        released => _lockHandles.TryRemove(released.LockId, out _));

                    // 与 Dispose 串行登记，防止提供者释放后遗留新取得的句柄
                    lock (_lifecycleLock)
                    {
                        ObjectDisposedException.ThrowIf(_disposed, this);
                        _lockHandles[handle.LockId] = handle;
                        transferred = true;
                    }

                    return handle;
                }
            }
            finally
            {
                if (!transferred)
                {
                    await redisLock.DisposeAsync();
                }
            }

            var remaining = wait - Stopwatch.GetElapsedTime(start);
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(interval < remaining ? interval : remaining, cancellationToken);
        } while (Stopwatch.GetElapsedTime(start) < wait);

        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("未获取到分布式锁: {ResourceId}, 等待窗口: {Wait}", resourceId, wait);
        return null;
    }

    public void Dispose()
    {
        DistributedLockHandle[] handles;
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            handles = _lockHandles.Values.ToArray();
        }

        foreach (var handle in handles)
        {
            try
            {
                handle.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "释放锁句柄时发生异常: {ResourceId}, {LockId}", handle.ResourceId, handle.LockId);
            }
        }
    }
}

using BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;
using BeniceSoft.Abp.Extensions.Redis;

namespace BeniceSoft.Abp.Extensions.DistributedLock;

/// <summary>
/// 按唯一所有权释放 Redis 锁，并避免工作单元的多个回调重复释放
/// </summary>
internal sealed class DistributedLockHandle : IDistributedLockHandle
{
    private IRedisLock? _redisLock;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly Action<DistributedLockHandle> _onDisposed;

    public string ResourceId { get; }
    public string LockId { get; }
    public bool IsAcquired => Volatile.Read(ref _redisLock)?.IsAcquired == true;

    public DistributedLockHandle(IRedisLock redisLock, Action<DistributedLockHandle> onDisposed)
    {
        _redisLock = redisLock;
        _onDisposed = onDisposed;
        ResourceId = redisLock.Resource;
        LockId = redisLock.LockId;
    }

    public void Dispose()
    {
        _lifecycle.Wait();
        try
        {
            var redisLock = Interlocked.Exchange(ref _redisLock, null);
            if (redisLock != null)
            {
                try
                {
                    redisLock.Dispose();
                }
                finally
                {
                    _onDisposed(this);
                }
            }
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            var redisLock = Interlocked.Exchange(ref _redisLock, null);
            if (redisLock != null)
            {
                try
                {
                    await redisLock.DisposeAsync();
                }
                finally
                {
                    _onDisposed(this);
                }
            }
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task<bool> RenewAsync(TimeSpan expires)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expires, TimeSpan.Zero);
        await _lifecycle.WaitAsync();
        try
        {
            return _redisLock != null &&
                   await _redisLock.ExtendAsync(expires) == RedisLockResult.Success;
        }
        finally
        {
            _lifecycle.Release();
        }
    }
}

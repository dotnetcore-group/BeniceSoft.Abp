using BeniceSoft.Core;
using Microsoft.Extensions.Caching.Memory;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface ICacheLockProvider
{
    int GetWaitSeconds();

    CacheItemPriority GetPriority();

    int GetEntrySize();

    object GetObject(object key);
}

internal sealed class CacheLockProvider : ICacheLockProvider
{
    private readonly ShardingOptions _options;
    private readonly object[] _locks;

    public CacheLockProvider(ShardingOptions options)
    {
        if (options.CacheConcurrencyLevel < 1)
        {
            throw new ShardingInvalidOperationException($"{options.CacheConcurrencyLevel} must > 0");
        }

        _options = options;
        _locks = new object[options.CacheConcurrencyLevel];
        foreach (var i in _locks.Length)
        {
            _locks[i] = new();
        }
    }

    public int GetEntrySize()
    {
        return _options.CacheEntrySize;
    }

    public object GetObject(object key)
    {
        if (_locks.Length == 1)
        {
            return _locks[0];
        }

        var hashCode = key.ToStringSafe().GetHashCode();
        var index = Math.Abs(hashCode % _locks.Length);
        return _locks[index];
    }

    public CacheItemPriority GetPriority()
    {
        return _options.CachePriority;
    }

    public int GetWaitSeconds()
    {
        return _options.CacheWaitSeconds;
    }
}

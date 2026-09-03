using System.Collections.Concurrent;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface ITrackerManager
{
    bool Add(Type entityType, bool hasKey);

    bool Contains(Type entityType);

    bool UseTrack(Type entityType);

    Type Translate(Type entityType);
}

internal sealed class TrackerManager(ShardingOptions options) : ITrackerManager
{
    private readonly ConcurrentDictionary<Type, bool> _models = new();

    public bool Add(Type entityType, bool hasKey)
    {
        return _models.TryAdd(entityType, hasKey);
    }

    public bool Contains(Type entityType)
    {
        if (_models.ContainsKey(entityType))
        {
            return true;
        }

        if (options.UseProxies && entityType.BaseType != null)
        {
            return _models.ContainsKey(entityType.BaseType);
        }

        return false;
    }

    public Type Translate(Type entityType)
    {
        if (options.UseProxies)
        {
            if (!_models.ContainsKey(entityType))
            {
                if (entityType.BaseType != null)
                {
                    if (_models.ContainsKey(entityType.BaseType))
                    {
                        return entityType.BaseType;
                    }
                }
            }
        }

        return entityType;
    }

    public bool UseTrack(Type entityType)
    {
        if (_models.TryGetValue(entityType, out var hasKey))
        {
            return hasKey;
        }

        if (options.UseProxies && entityType.BaseType != null)
        {
            if (_models.TryGetValue(entityType.BaseType, out hasKey))
            {
                return hasKey;
            }
        }

        return false;
    }
}

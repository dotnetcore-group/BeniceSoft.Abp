using BeniceSoft.Core;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IRouteTail
{
    string Identity { get; }

    bool MultipleQuery { get; }

    bool ShardingTable { get; }
}

/// <summary>
/// 不缓存模型
/// </summary>
public interface INoCacheRouteTail
{
}

public interface ISingleRouteTail : IRouteTail
{
    string Tail { get; }
}

public class SingleRouteTail : ISingleRouteTail
{
    public SingleRouteTail(TableRouteResult tableRouteResult)
    {
        if (tableRouteResult.ReplaceTables.IsNull() || tableRouteResult.ReplaceTables.Count > 1)
        {
            throw new ArgumentException("route result replace tables must equals 1");
        }

        Tail = tableRouteResult.ReplaceTables.First().Tail;
        Identity = Tail.RouteTail();
        ShardingTable = Tail.IsNotNull();
    }

    public SingleRouteTail(string tail)
    {
        Tail = tail;
        Identity = Tail.RouteTail();
        ShardingTable = Tail.IsNotNull();
    }

    public string Tail { get; }

    public string Identity { get; }

    public bool MultipleQuery => false;

    public bool ShardingTable { get; }
}

public class NoCacheSingleRouteTail : SingleRouteTail, INoCacheRouteTail
{
    public NoCacheSingleRouteTail(TableRouteResult tableRouteResult) : base(tableRouteResult)
    {
    }

    public NoCacheSingleRouteTail(string tail) : base(tail)
    {
    }
}

/// <summary>
/// 多模型，例如Join
/// </summary>
public interface IMultipleRouteTail : IRouteTail, INoCacheRouteTail
{
    ISet<Type> EntityTypes { get; }

    string GetTail(Type type);
}

public class MultipleRouteTail : IMultipleRouteTail
{
    private readonly TableRouteResult _tableRouteResult;

    public MultipleRouteTail(TableRouteResult tableRouteResult, bool shardingTable)
    {
        if (tableRouteResult.ReplaceTables.IsNull() || tableRouteResult.ReplaceTables.Count <= 1)
        {
            throw new ArgumentException("route result replace tables must greater than 1");
        }

        Identity = $"RANDOM_SHARDING_MODEL_CACHE_KEY_{RandomUtils.GuidString()}";
        EntityTypes = tableRouteResult.ReplaceTables.Select(o => o.EntityType).ToHashSet();
        _tableRouteResult = tableRouteResult;
        ShardingTable = shardingTable;
    }

    public ISet<Type> EntityTypes { get; }

    public string Identity { get; }

    public bool MultipleQuery => true;

    public bool ShardingTable { get; }

    public string GetTail(Type type)
    {
        return _tableRouteResult.ReplaceTables.Single(o => o.EntityType == type).Tail;
    }
}

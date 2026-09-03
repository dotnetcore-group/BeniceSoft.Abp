using BeniceSoft.Core;
using BeniceSoft.Core.Strategy;
using System.Collections.Concurrent;
using System.Data.SqlTypes;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 分表内存排序比较器
/// </summary>
public interface IShardingComparer
{
    /// <summary>
    /// 比较 参数
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="direction"></param>
    /// <returns></returns>
    int Compare(IComparable a, IComparable b, SortDirection direction);

    /// <summary>
    /// 创建一个比较器
    /// </summary>
    /// <param name="comparerType"></param>
    /// <returns></returns>
    object CreateComparer(Type comparerType);
}

internal sealed class ShardingComparer : IShardingComparer
{
    private readonly ConcurrentDictionary<Type, object> _comparers = new();

    public int Compare(IComparable a, IComparable b, SortDirection direction)
    {
        if (a is Guid ga && b is Guid gb)
        {
            return new SqlGuid(ga).SafeCompare(new SqlGuid(gb), direction);
        }

        return a.SafeCompare(b, direction);
    }

    public object CreateComparer(Type comparerType)
    {
        var comparer = _comparers.GetOrAdd(comparerType, key => Activator.CreateInstance(typeof(ShardingMemoryComparer<>).MakeGenericType(comparerType), this)
            ?? throw new InvalidOperationException($"Unable to create comparer for type [{comparerType}]."));
        return comparer;
    }
}

internal sealed class ShardingMemoryComparer<T>(IShardingComparer shardingComparer) : IComparer<T>
{
    public int Compare(T? x, T? y)
    {
        if (x is IComparable a && y is IComparable b)
        {
            return shardingComparer.Compare(a, b, SortDirection.Ascending);
        }

        throw new NotImplementedException($"compare :[{typeof(T).FullName}] is not IComparable");
    }
}

internal sealed class NoShardingComparer : IComparer<string>
{
    private readonly string _tail = new SingleRouteTail(string.Empty).Identity;

    public int Compare(string? x, string? y)
    {
        if (!object.Equals(x, y))
        {
            if (x is not null && _tail.EqualsTo(x))
            {
                return -1;
            }

            if (y is not null && _tail.EqualsTo(y))
            {
                return 1;
            }
        }

        return Comparer<string>.Default.Compare(x, y);
    }
}

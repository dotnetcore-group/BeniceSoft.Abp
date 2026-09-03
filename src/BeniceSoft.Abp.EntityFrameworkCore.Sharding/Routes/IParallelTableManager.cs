using BeniceSoft.Core;
namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IParallelTableManager
{
    /// <summary>
    /// 添加平行表
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    bool Add(ParallelTableNode node);

    /// <summary>
    /// 是否是平行表查询
    /// </summary>
    /// <param name="entityTypes"></param>
    /// <returns></returns>
    bool IsQuery(IEnumerable<Type> entityTypes);

    /// <summary>
    /// 是否是平行表查询
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    bool IsQuery(ParallelTableNode node);
}

internal sealed class ParallelTableManager : IParallelTableManager
{
    private readonly HashSet<ParallelTableNode> _nodes = [];

    public bool Add(ParallelTableNode node)
    {
        return _nodes.Add(node);
    }

    public bool IsQuery(IEnumerable<Type> entityTypes)
    {
        if (entityTypes.IsNull())
        {
            return false;
        }

        var node = new ParallelTableNode(entityTypes.Select(o => new ParallelTableComparer(o)));

        return IsQuery(node);
    }

    public bool IsQuery(ParallelTableNode node)
    {
        if (node == null)
        {
            return false;
        }

        return _nodes.Contains(node);
    }
}

/// <summary>
/// 平行表组节点用来表示一组平行表
/// </summary>
public sealed class ParallelTableNode(IEnumerable<ParallelTableComparer> entities)
{
    public ISet<ParallelTableComparer> Entities { get; } = new SortedSet<ParallelTableComparer>(entities);

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Entities.SequenceEqual(((ParallelTableNode)obj).Entities);
    }

    public override int GetHashCode()
    {
        return Entities.Sum(x => x.GetHashCode());
    }
}

/// <summary>
/// 单张表对象类型比较器
/// </summary>
public sealed class ParallelTableComparer(Type type) : IComparable<ParallelTableComparer>, IComparable
{
    public Type Type { get; } = type;

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals(Type, ((ParallelTableComparer)obj).Type);
    }

    public override int GetHashCode()
    {
        return Type?.GetHashCode() ?? 0;
    }

    public int CompareTo(ParallelTableComparer? other)
    {
        if (Type == null)
        {
            return -1;
        }

        if (other == null)
        {
            return 1;
        }

        if (other.Type == null)
        {
            return 1;
        }

        return GetHashCode() - other.GetHashCode();
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as ParallelTableComparer);
    }

    public static bool operator ==(ParallelTableComparer? left, ParallelTableComparer? right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    public static bool operator !=(ParallelTableComparer? left, ParallelTableComparer? right)
    {
        return !(left == right);
    }

    public static bool operator <(ParallelTableComparer? left, ParallelTableComparer? right)
    {
        return left is null ? right is not null : left.CompareTo(right) < 0;
    }

    public static bool operator <=(ParallelTableComparer? left, ParallelTableComparer? right)
    {
        return left is null || left.CompareTo(right) <= 0;
    }

    public static bool operator >(ParallelTableComparer? left, ParallelTableComparer? right)
    {
        return left is not null && left.CompareTo(right) > 0;
    }

    public static bool operator >=(ParallelTableComparer? left, ParallelTableComparer? right)
    {
        return left is null ? right is null : left.CompareTo(right) >= 0;
    }
}

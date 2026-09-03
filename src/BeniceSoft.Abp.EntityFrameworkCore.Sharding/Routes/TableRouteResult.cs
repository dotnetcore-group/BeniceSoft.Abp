using BeniceSoft.Core;
namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public sealed class TableRouteResult
{
    public TableRouteResult(IEnumerable<TableRouteUnit> tables)
    {
        tables ??= [];
        ReplaceTables = tables.ToHashSet();
        IsEmpty = tables.IsNull();
        HasDifferentTail = !IsEmpty && ReplaceTables.GroupBy(o => o.Tail).Count() != 1;
    }

    public ISet<TableRouteUnit> ReplaceTables { get; }

    public bool HasDifferentTail { get; }

    public bool IsEmpty { get; }

    public override string ToString()
    {
        return $"(has different tail:{HasDifferentTail},current table:[{ReplaceTables.Select(o => $"{o.DataSource}.{o.Tail}.{o.EntityType}").JoinStr()}])";
    }

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

        var other = (TableRouteResult)obj;
        var result = HasDifferentTail == other.HasDifferentTail && IsEmpty == other.IsEmpty && Equals(ReplaceTables, other.ReplaceTables);
        return result;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ReplaceTables, HasDifferentTail, IsEmpty);
    }
}

internal sealed class TableRouteRuleContext(DataSourceRouteResult routeResult, IQueryable queryable, Dictionary<Type, IQueryable?> entities)
{
    public DataSourceRouteResult RouteResult { get; } = routeResult;

    public IQueryable Queryable { get; } = queryable;

    public Dictionary<Type, IQueryable?> Entities { get; } = entities;
}

public sealed class TableRouteUnit(string dataSource, string tail, Type entityType)
{
    public string DataSource { get; } = dataSource;

    public string Tail { get; } = tail;

    public Type EntityType { get; } = entityType;

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj is TableRouteUnit other)
        {
            return DataSource == other.DataSource && Tail == other.Tail && Equals(EntityType, other.EntityType);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(DataSource, Tail, EntityType);
    }
}

using BeniceSoft.Core;
namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public sealed class DataSourceRouteResult(ISet<string> intersect)
{
    public DataSourceRouteResult(string dataSource) : this(new HashSet<string>() { dataSource })
    {
    }

    /// <summary>
    /// 交集
    /// </summary>
    public ISet<string> Intersect { get; } = intersect;

    public override string ToString()
    {
        return $"data source route result:{Intersect.JoinStr()}";
    }
}

internal sealed class DataSourceRouteRuleContext(IQueryable queryable, IShardingDbContext ctx, Dictionary<Type, IQueryable?> entities)
{
    public Dictionary<Type, IQueryable?> Entities { get; } = entities;

    /// <summary>
    /// 查询条件
    /// </summary>
    public IQueryable Queryable { get; } = queryable;

    public IShardingDbContext DbContext { get; } = ctx;
}

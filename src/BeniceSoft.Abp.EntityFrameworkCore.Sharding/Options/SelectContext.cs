using BeniceSoft.Core;
using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal sealed class SelectContext
{
    public List<SelectOwnerProperty> Properties { get; } = [];

    public bool HasAverage => Properties.Exists(o => o is SelectAverageProperty);

    public override string ToString()
    {
        return Properties.JoinStr();
    }
}

internal sealed class OrderByContext
{
    public LinkedList<PropertySorting> Sorts { get; } = new();

    public string GetExpression()
    {
        return Sorts.JoinStr();
    }
}

internal sealed class GroupByContext
{
    /// <summary>
    /// group by 表达式
    /// </summary>
    public LambdaExpression? Expression { get; set; }

    /// <summary>
    /// 是否内存聚合
    /// </summary>
    public bool MemoryMerge { get; set; }

    public List<PropertySorting> Sorts { get; } = [];

    public string GetExpression()
    {
        return Sorts.JoinStr();
    }
}

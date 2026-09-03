using BeniceSoft.Core;
using System.Linq.Expressions;
using System.Reflection;
using BeniceSoft.Core.Strategy;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 分页配置元数据
/// </summary>
public sealed class PagedMetadata
{
    public ISet<PagedSequenceOptions> Sequences { get; } = new HashSet<PagedSequenceOptions>();

    /// <summary>
    /// 反向排序因子 skip>ReverseFactor * total 
    /// </summary>
    public double ReverseFactor { get; set; } = -1;

    /// <summary>
    /// 当条数大于ReverseTotalGe条后采用反向排序
    /// </summary>
    public long ReverseTotalGe { get; set; } = 10000L;
    /// <summary>
    /// 是否已开启反向排序  skip>ReverseFactor * total  查询条件必须存在 order by
    /// </summary>
    public bool EnableReverse => ReverseFactor > 0 && ReverseFactor < 1 && ReverseTotalGe >= 500;

    public IMultipleQuery? MultipleQuery { get; set; }

    /// <summary>
    /// 是否启用多次查询
    /// </summary>
    public bool EnabledMultipleQuery => MultipleQuery != null;

    internal bool UseReverse(int skip, long total)
    {
        if (total < ReverseTotalGe)
        {
            return false;
        }

        return skip > ReverseFactor * total;
    }
}

public sealed class PagedSequenceOptions
{
    public PagedSequenceOptions(LambdaExpression expression, PagedMatchMode mode = PagedMatchMode.Owner, IComparer<string>? routeComparer = null)
    {
        OrderProperty = expression.GetProperty();
        PropertyName = OrderProperty.Name;
        MatchMode = mode;
        RouteComparer = routeComparer ?? Comparer<string>.Default;
        SequenceTails = new HashSet<string>();
    }

    public IComparer<string> RouteComparer { get; set; }

    public PagedMatchMode MatchMode { get; set; }

    public PropertyInfo OrderProperty { get; set; }

    /// <summary>
    /// 如果查询没发现排序就将当前配置追加上去
    /// </summary>
    public bool DefaultAppended => Order >= 0;

    /// <summary>
    /// 大于等于0表示需要
    /// </summary>
    public int Order { get; set; } = -1;

    public SortDirection Direction { get; set; } = SortDirection.Ascending;

    public string PropertyName { get; }

    public ISet<string> SequenceTails { get; }

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

        if (obj is PagedSequenceOptions other)
        {
            return PropertyName == other.PropertyName;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return PropertyName != null ? PropertyName.GetHashCode() : 0;
    }
}

[Flags]
public enum PagedMatchMode
{
    /// <summary>
    /// 必须是当前对象的属性
    /// </summary>
    Owner = 1,

    /// <summary>
    /// 只要名称一样就可以了
    /// </summary>
    Named = 1 << 1,

    /// <summary>
    /// 仅第一个匹配就可以了
    /// </summary>
    PrimaryMatch = 1 << 2
}

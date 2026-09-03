using System.Linq.Expressions;
using BeniceSoft.Core.Strategy;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public sealed class PagedBuilder<T>(PagedMetadata metadata)
    where T : class
{

    /// <summary>
    /// 分页排序
    /// </summary>
    /// <typeparam name="TProperty"></typeparam>
    /// <param name="expression"></param>
    /// <returns></returns>
    public PagedSortingBuilder Sort<TProperty>(Expression<Func<T, TProperty>> expression)
    {
        return new PagedSortingBuilder(expression, metadata);
    }

    /// <summary>
    /// 配置反向排序
    /// </summary>
    /// <param name="reverseFactor"></param>
    /// <param name="reverseTotalGe"></param>
    /// <returns></returns>
    public PagedBuilder<T> ReversePage(double reverseFactor = 0.5, long reverseTotalGe = 10000L)
    {
        metadata.ReverseFactor = reverseFactor;
        metadata.ReverseTotalGe = reverseTotalGe;
        return this;
    }

    /// <summary>
    /// 启用多次查询排序
    /// </summary>
    /// <returns></returns>
    public PagedBuilder<T> SetMultipleQuery(IMultipleQuery query)
    {
        metadata.MultipleQuery = query;
        return this;
    }
}

public sealed class PagedSortingBuilder
{
    private readonly PagedSequenceOptions _sequence;

    public PagedSortingBuilder(LambdaExpression expression, PagedMetadata metadata)
    {
        _sequence = new PagedSequenceOptions(expression);
        metadata.Sequences.Add(_sequence);
    }

    /// <summary>
    /// 使用哪个后缀比较
    /// 设置的比较器是asc的情况下
    /// </summary>
    /// <param name="routeComparer"></param>
    /// <returns></returns>
    public PagedSortingBuilder WithRouteComparer(IComparer<string> routeComparer)
    {
        ArgumentNullException.ThrowIfNull(routeComparer);

        _sequence.RouteComparer = routeComparer;
        return this;
    }

    /// <summary>
    /// 使用哪种比较方式
    /// </summary>
    /// <param name="mode"></param>
    /// <returns></returns>
    public PagedSortingBuilder WithMatchMode(PagedMatchMode mode)
    {
        _sequence.MatchMode = mode;
        return this;
    }

    /// <summary>
    /// 如果查询没发现排序就将当前配置追加上去
    /// </summary>
    /// <param name="order">大于等于0生效,越大优先级越高</param>
    /// <param name="direction">默认asc还是desc</param>
    /// <returns></returns>
    public PagedSortingBuilder WithSort(int order, SortDirection direction = SortDirection.Ascending)
    {
        _sequence.Order = order;
        _sequence.Direction = direction;
        return this;
    }
}

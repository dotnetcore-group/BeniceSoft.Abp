using BeniceSoft.Core;
using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public sealed class EntityQueryBuilder<T>(EntityQueryMetadata metadata)
    where T : class
{

    /// <summary>
    /// 添加分表后缀排序
    /// </summary>
    /// <param name="tailComparer"></param>
    /// <param name="reverse">是否降序</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public EntityQueryBuilder<T> WithTailComparer(IComparer<string> tailComparer, bool reverse = true)
    {
        ArgumentNullException.ThrowIfNull(tailComparer);

        metadata.DefaultTailComparer = tailComparer;
        metadata.Reverse = reverse;
        return this;
    }

    /// <summary>
    /// 添加和默认数据库排序一样的排序
    /// </summary>
    /// <param name="propertyName"></param>
    /// <param name="sameTailComparer"></param>
    /// <param name="value"></param>
    public EntityQueryBuilder<T> AddSequence<TProperty>(Expression<Func<T, TProperty>> propertyName, bool sameTailComparer, SequenceMatchMode value)
    {
        metadata.AddSequence(propertyName.GetProperty().Name, sameTailComparer, value);
        return this;
    }

    /// <summary>
    /// 添加链接限制,和程序启动配置的MaxQueryConnections取最小值,非迭代器有效,
    /// </summary>
    /// <param name="limit"></param>
    /// <param name="values"></param>
    public EntityQueryBuilder<T> AddLimit(int limit, params ShardingLimit[] values)
    {
        if (limit < 1)
        {
            throw new ArgumentException($"{nameof(limit)} should >= 1");
        }

        if (values.IsNull())
        {
            throw new ArgumentNullException(nameof(values));
        }

        foreach (var value in values)
        {
            metadata.AddLimit(limit, value);
        }

        return this;
    }

    /// <summary>
    /// 配置默认方法不带排序的时候采用什么排序来触发熔断
    /// </summary>
    /// <param name="sameTailComparer">true表示和默认的ShardingTailComparer排序一致,false表示和默认的排序相反</param>
    /// <param name="values"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public EntityQueryBuilder<T> AddDefault(bool sameTailComparer, CircuitBreaker[] values)
    {
        if (values.IsNull())
        {
            throw new ArgumentNullException(nameof(values));
        }

        foreach (var value in values)
        {
            metadata.AddDefault(sameTailComparer, value);
        }

        return this;
    }
}

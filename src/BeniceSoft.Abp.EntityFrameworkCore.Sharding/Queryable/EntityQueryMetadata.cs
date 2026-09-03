namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public sealed class EntityQueryMetadata
{
    public const string Enumerator = "Enumerator";

    private static readonly Dictionary<CircuitBreaker, string> _circuitBreaker = new()
    {
        { CircuitBreaker.First, nameof(Queryable.First) },
        { CircuitBreaker.FirstOrDefault, nameof(Queryable.FirstOrDefault) },
        { CircuitBreaker.Last, nameof(Queryable.Last) },
        { CircuitBreaker.LastOrDefault, nameof(Queryable.LastOrDefault) },
        { CircuitBreaker.Single, nameof(Queryable.Single) },
        { CircuitBreaker.SingleOrDefault, nameof(Queryable.SingleOrDefault) },
        { CircuitBreaker.Any, nameof(Queryable.Any) },
        { CircuitBreaker.All, nameof(Queryable.All) },
        { CircuitBreaker.Contains, nameof(Queryable.Contains) },
        { CircuitBreaker.Enumerator,Enumerator }
    };

    private static readonly Dictionary<ShardingLimit, string> _limit = new()
    {
        { ShardingLimit.First, nameof(Queryable.First) },
        { ShardingLimit.FirstOrDefault, nameof(Queryable.FirstOrDefault) },
        { ShardingLimit.Last, nameof(Queryable.Last) },
        { ShardingLimit.LastOrDefault, nameof(Queryable.LastOrDefault) },
        { ShardingLimit.Single, nameof(Queryable.Single) },
        { ShardingLimit.SingleOrDefault, nameof(Queryable.SingleOrDefault) },
        { ShardingLimit.Any, nameof(Queryable.Any) },
        { ShardingLimit.All, nameof(Queryable.All) },
        { ShardingLimit.Contains, nameof(Queryable.Contains) },
        { ShardingLimit.Max, nameof(Queryable.Max) },
        { ShardingLimit.Min, nameof(Queryable.Min) },
        { ShardingLimit.Count, nameof(Queryable.Count) },
        { ShardingLimit.LongCount, nameof(Queryable.LongCount) },
        { ShardingLimit.Sum, nameof(Queryable.Sum) },
        { ShardingLimit.Average, nameof(Queryable.Average) },
        { ShardingLimit.Enumerator, Enumerator }
    };

    private readonly Dictionary<string, SequenceQueryMatch> _sequence = [];
    private readonly Dictionary<string, int> _sequenceLimit = [];
    private readonly Dictionary<string, bool> _sequenceDefault = [];

    public IComparer<string> DefaultTailComparer { get; set; } = Comparer<string>.Default;

    public bool Reverse { get; set; } = true;

    /// <summary>
    /// 添加和默认数据库排序一样的排序
    /// </summary>
    /// <param name="propertyName"></param>
    /// <param name="sameTailComparer"></param>
    /// <param name="value"></param>
    public void AddSequence(string propertyName, bool sameTailComparer, SequenceMatchMode value)
    {
        _sequence[propertyName] = new SequenceQueryMatch(sameTailComparer, value);
    }

    /// <summary>
    /// 是否包含当前排序字段
    /// </summary>
    /// <param name="propertyName"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool TryGetSequence(string propertyName, out SequenceQueryMatch? value)
    {
        if (_sequence.TryGetValue(propertyName, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// 添加对应方法的连接数限制
    /// </summary>
    /// <param name="limit"></param>
    /// <param name="value"></param>
    public void AddLimit(int limit, ShardingLimit value)
    {
        if (!_limit.TryGetValue(value, out var methodName))
        {
            throw new ArgumentException(value.ToString());
        }

        _sequenceLimit[methodName] = limit;
    }

    /// <summary>
    /// 尝试获取当前查询方法配置的连接数限制
    /// </summary>
    /// <param name="methodName">First、FirstOrDefault...</param>
    /// <param name="value">连接数限制</param>
    /// <returns></returns>
    public bool TryGetLimit(string methodName, out int value)
    {
        if (_sequenceLimit.TryGetValue(methodName, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// 默认顺序查询熔断
    /// </summary>
    /// <param name="sameTailComparer"></param>
    /// <param name="value"></param>
    /// <exception cref="ArgumentException"></exception>
    public void AddDefault(bool sameTailComparer, CircuitBreaker value)
    {
        if (!_circuitBreaker.TryGetValue(value, out var methodName))
        {
            throw new ArgumentException(value.ToString());
        }

        _sequenceDefault[methodName] = sameTailComparer;
    }

    /// <summary>
    /// 当前方法是否配置了顺序排序查询熔断
    /// </summary>
    /// <param name="value"></param>
    /// <param name="methodName"></param>
    /// <returns></returns>
    public bool TryGetDefault(string methodName, out bool value)
    {
        if (_sequenceDefault.TryGetValue(methodName, out value))
        {
            return true;
        }

        value = false;
        return false;
    }
}

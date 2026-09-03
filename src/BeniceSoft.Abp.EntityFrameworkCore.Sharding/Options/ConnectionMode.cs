namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 链接模式,可以由用户自行指定，使用内存限制或连接数限制或者系统自行选择最优
/// </summary>
public enum ConnectionMode
{
    /// <summary>
    /// 系统自行选择会根据用户的配置采取最小化连接数，但是如果遇到分页则会根据分页策略采取内存限制，因为skip过大会导致内存爆炸
    /// </summary>
    Automatic,

    /// <summary>
    /// 最小化内存使用率,就是非一次性获取所有数据然后采用流式聚合,同时会有多个链接
    /// </summary>
    MemoryStrictly,

    /// <summary>
    /// 最小化连接并发数，就是单次查询并发连接数为设置的连接数<see cref="ShardingOptions.MaxQueryConnections"/>。因为有限制，所以无法一直挂起连接，必须全部获取到内存后进行内存聚合,连接数会有限制
    /// </summary>
    ConnectionStrictly
}

public enum CreateDbStrategy
{
    /// <summary>
    /// 共享链接(只是用写链接字符串) 无需管理Connection的生命周期
    /// </summary>
    Share = 0,

    /// <summary>
    /// 并行查询链接(有可能会使用读写分离链接字符串) 独立生命周期
    /// </summary>
    ParallelQuery = 1,

    /// <summary>
    /// 并行写链接(只是用写链接字符串) 独立生命周期
    /// </summary>
    ParallelWrite = 2
}

/// <summary>
/// 可以熔断的方法名
/// </summary>
public enum CircuitBreaker
{
    First,
    FirstOrDefault,
    Last,
    LastOrDefault,
    Single,
    SingleOrDefault,
    Any,
    All,
    Contains,
    Enumerator
}

/// <summary>
/// 配置限制最大连接数的方法名
/// </summary>
public enum ShardingLimit
{
    First,
    FirstOrDefault,
    Last,
    LastOrDefault,
    Single,
    SingleOrDefault,
    Any,
    All,
    Contains,
    Max,
    Min,
    Count,
    LongCount,
    Sum,
    Average,
    Enumerator
}

[Flags]
public enum SequenceMatchMode
{
    /// <summary>
    /// 所属对象
    /// </summary>
    Owner = 1,

    /// <summary>
    /// 所属排序名称一样
    /// </summary>
    Named = 1 << 1
}

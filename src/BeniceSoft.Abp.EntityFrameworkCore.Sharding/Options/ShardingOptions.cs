using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public class ShardingOptions(Action<DbContextOptionsBuilder>? buildFactory = null)
{
    /// <summary>
    /// 模型缓存锁等待时间
    /// </summary>
    public int CacheWaitSeconds { get; set; } = 3;

    /// <summary>
    /// 模型缓存优先级
    /// </summary>
    public CacheItemPriority CachePriority { get; set; } = CacheItemPriority.High;

    /// <summary>
    /// efcore缓存最多限制10240个，单个缓存size设置为10那么就意味可以最多同一时间缓存1024个(缓存过期了那么还是可以缓存进去)
    /// </summary>
    public int CacheEntrySize { get; set; } = 1;

    /// <summary>
    /// 模型缓存锁等级
    /// </summary>
    public int CacheConcurrencyLevel { get; set; } = 1;

    /// <summary>
    /// 是否使用代理模式
    /// </summary>
    public bool UseProxies { get; set; }

    /// <summary>
    /// 写操作数据库后自动使用写库链接防止读库链接未同步无法查询到数据
    /// </summary>
    public bool AutoUseWriteDb { get; set; }

    /// <summary>
    /// 当查询遇到没有路由被命中时是否抛出错误
    /// </summary>
    public bool ThrowRouteNotMatch { get; set; } = true;

    /// <summary>
    /// 忽略建表时的错误
    /// </summary>
    public bool IgnoreCreateTableError { get; set; }

    /// <summary>
    /// 配置全局迁移最大并行数,以data source为一个单元并行迁移保证在多数据库分库情况下可以大大提高性能
    /// 默认系统逻辑处理器<code>Environment.ProcessorCount</code>
    /// </summary>
    public int MigrationParallelCount { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// 启动补偿表的最大并行数,以data source为一个单元并行迁移保证在多数据库分库情况下可以大大提高性能
    /// 默认系统逻辑处理器<code>Environment.ProcessorCount</code>
    /// </summary>
    public int CompensateTableParallelCount { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// 全局配置最大的查询连接数限制,默认系统逻辑处理器<code>Environment.ProcessorCount</code>
    /// </summary>
    public int MaxQueryConnections { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// 连接模式
    /// </summary>
    public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.Automatic;

    /// <summary>
    /// 读写分离配置
    /// </summary>
    public SeparationOptions? Separation { get; set; }

    /// <summary>
    /// 默认数据源
    /// </summary>
    public string DefaultDataSource { get; set; } = string.Empty;

    /// <summary>
    /// 默认数据源链接字符串
    /// </summary>
    public string DefaultConnection { get; set; } = string.Empty;

    /// <summary>
    /// 检测分片键的自动值是否有疑义
    /// </summary>
    public bool Doubt { get; set; }

    /// <summary>
    /// 额外数据源配置
    /// </summary>
    public Func<IShardingProvider, IDictionary<string, string>>? AdditionalDataSourceFactory { get; set; }

    /// <summary>
    /// 多个DbContext事务传播委托
    /// </summary>
    public Action<DbConnection, DbContextOptionsBuilder>? ConnectionFactory { get; private set; }

    /// <summary>
    /// 初始DbContext的创建委托
    /// </summary>
    public Action<string, DbContextOptionsBuilder>? ConnectionStringFactory { get; private set; }

    /// <summary>
    /// 外部DbContext的配置委托
    /// </summary>
    public Action<DbContextOptionsBuilder>? ShellDbContextFactory { get; private set; }

    /// <summary>
    /// 仅内部真正执行的DbContext生效的配置委托
    /// </summary>
    public Action<DbContextOptionsBuilder>? ExecutorDbContextFactory { get; private set; }

    /// <summary>
    /// 分片迁移使用的配置
    /// </summary>

    public Action<DbContextOptionsBuilder>? MigrationFactory { get; private set; }

    /// <summary>
    /// 设置默认数据源
    /// </summary>
    /// <param name="name"></param>
    /// <param name="connectionString"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ShardingOptions WithDefaultDataSource(string name, string connectionString)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(connectionString);

        DefaultDataSource = name;
        DefaultConnection = connectionString;
        return this;
    }

    /// <summary>
    /// 如何使用字符串创建DbContext
    /// </summary>
    /// <param name="queryFactory"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public void WithShardingQuery(Action<string, DbContextOptionsBuilder> queryFactory)
    {
        ArgumentNullException.ThrowIfNull(queryFactory);

        void Use(string c, DbContextOptionsBuilder b)
        {
            queryFactory(c, b);
            buildFactory?.Invoke(b);
        }

        ConnectionStringFactory = Use;
    }

    /// <summary>
    /// 如何传递事务到其他DbContext
    /// </summary>
    /// <param name="transactionFactory"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public void WithShardingTransaction(Action<DbConnection, DbContextOptionsBuilder> transactionFactory)
    {
        ArgumentNullException.ThrowIfNull(transactionFactory);

        void Use(DbConnection c, DbContextOptionsBuilder b)
        {
            transactionFactory(c, b);
            buildFactory?.Invoke(b);
        }

        ConnectionFactory = Use;
    }

    internal void CheckLegality()
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(DefaultDataSource);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(DefaultConnection);
        ArgumentNullException.ThrowIfNull(ConnectionStringFactory);
        ArgumentNullException.ThrowIfNull(ConnectionFactory);

        if (MaxQueryConnections < 1)
        {
            throw new ArgumentException($"{nameof(MaxQueryConnections)} should greater than and equal 1");
        }
    }
}

using BeniceSoft.Core;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IVirtualDataSource
{
    /// <summary>
    /// 数据源配置
    /// </summary>
    IVirtualDataSourceOptions Options { get; }

    /// <summary>
    /// 链接字符串管理
    /// </summary>
    IConnectionManager ConnectionManager { get; }

    /// <summary>
    /// 是否启用了读写分离
    /// </summary>
    bool UseSeparation { get; }

    /// <summary>
    /// 默认的数据源名称
    /// </summary>
    string DefaultDataSource { get; }

    /// <summary>
    /// 默认连接字符串
    /// </summary>
    string DefaultConnection { get; }

    /// <summary>
    /// 获取默认的数据源信息
    /// </summary>
    /// <returns></returns>
    IPhysicDataSource GetDefaultDataSource();

    /// <summary>
    /// 获取数据源
    /// </summary>
    /// <param name="dataSource"></param>
    /// <exception cref="ShardingNotFoundException">
    ///     thrown if data source name is not in virtual data source
    ///     the length of the buffer
    /// </exception>
    /// <returns></returns>
    IPhysicDataSource GetPhysicDataSource(string dataSource);

    /// <summary>
    /// 获取所有的数据源名称
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<string> GetAllDataSource();

    /// <summary>
    /// 获取连接字符串
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="ShardingNotFoundException"></exception>
    string GetConnectionString(string name);

    /// <summary>
    /// 添加数据源
    /// </summary>
    /// <param name="physicDataSource"></param>
    /// <returns></returns>
    /// <exception cref="ShardingInvalidOperationException">重复添加默认数据源</exception>
    bool AddPhysicDataSource(IPhysicDataSource physicDataSource);

    /// <summary>
    /// 是否默认数据源
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    bool IsDefault(string name);

    /// <summary>
    /// 检查是否配置默认数据源和默认链接字符串
    /// </summary>
    /// <exception cref="ShardingInvalidOperationException"></exception>
    void CheckVirtualDataSource();

    /// <summary>
    /// 如何根据connectionString 配置 DbContextOptionsBuilder
    /// </summary>
    /// <param name="connectionString"></param>
    /// <param name="dbContextOptionsBuilder"></param>
    /// <returns></returns>
    DbContextOptionsBuilder UseDbContextOptionsBuilder(string connectionString, DbContextOptionsBuilder dbContextOptionsBuilder);

    /// <summary>
    /// 如何根据dbConnection 配置DbContextOptionsBuilder
    /// </summary>
    /// <param name="dbConnection"></param>
    /// <param name="dbContextOptionsBuilder"></param>
    /// <returns></returns>
    DbContextOptionsBuilder UseDbContextOptionsBuilder(DbConnection dbConnection, DbContextOptionsBuilder dbContextOptionsBuilder);

    IReadOnlyDictionary<string, string> GetDataSource();
}

internal sealed class VirtualDataSource : IVirtualDataSource
{
    private readonly PhysicDataSourcePool _physicDataSourcePool;

    public IVirtualDataSourceOptions Options { get; }

    public IConnectionManager ConnectionManager { get; }

    public string DefaultDataSource { get; private set; } = null!;

    public string DefaultConnection { get; private set; } = null!;

    public bool UseSeparation { get; }

    public VirtualDataSource(IVirtualDataSourceOptions options, ISeparationConnectionFactory factory)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.AdditionalDataSource == null)
        {
            throw new ArgumentException(nameof(options.AdditionalDataSource) + " is null");
        }

        if (options.MaxQueryConnections <= 0)
        {
            throw new ArgumentException(nameof(options.MaxQueryConnections) + "is out of range");
        }

        Options = options;
        _physicDataSourcePool = new PhysicDataSourcePool();
        //添加数据源
        AddPhysicDataSource(new PhysicDataSource(Options.DefaultDataSource, Options.DefaultConnection, true));
        foreach (var extraDataSource in Options.AdditionalDataSource)
        {
            AddPhysicDataSource(new PhysicDataSource(extraDataSource.Key, extraDataSource.Value, false));
        }

        UseSeparation = Options.UseSeparation();
        if (UseSeparation)
        {
            ConnectionManager = new SeparationConnectionManager(this, factory);
        }
        else
        {
            ConnectionManager = new ConnectionManager(this);

        }
    }

    /// <summary>
    /// 获取默认数据源
    /// </summary>
    /// <returns></returns>
    public IPhysicDataSource GetDefaultDataSource()
    {
        return GetPhysicDataSource(DefaultDataSource);
    }

    /// <summary>
    /// 获取物理数据源
    /// </summary>
    /// <param name="dataSource"></param>
    /// <returns></returns>
    /// <exception cref="ShardingNotFoundException"></exception>
    public IPhysicDataSource GetPhysicDataSource(string dataSource)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(dataSource);

        var source = _physicDataSourcePool.TryGet(dataSource);
        if (source == null)
        {
            throw new ShardingNotFoundException($"data source:[{source}]");
        }

        return source;
    }
    /// <summary>
    /// 获取所有的数据源名称
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<string> GetAllDataSource()
    {
        return _physicDataSourcePool.GetAllDataSource();
    }

    /// <summary>
    /// 根据数据源名称获取链接字符串
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public string GetConnectionString(string name)
    {
        if (IsDefault(name))
        {
            return DefaultConnection;
        }

        return GetPhysicDataSource(name).ConnectionString;
    }

    /// <summary>
    /// 添加物理数据源
    /// </summary>
    /// <param name="physicDataSource"></param>
    /// <returns></returns>
    public bool AddPhysicDataSource(IPhysicDataSource physicDataSource)
    {
        if (physicDataSource.IsDefault)
        {
            if (DefaultDataSource.IsNotNull())
            {
                throw new ShardingInvalidOperationException($"default data source name:[{DefaultDataSource}],add physic default data source name:[{physicDataSource.Name}]");
            }

            DefaultDataSource = physicDataSource.Name;
            DefaultConnection = physicDataSource.ConnectionString;
        }

        return _physicDataSourcePool.TryAdd(physicDataSource);
    }
    /// <summary>
    /// 判断数据源名称是否是默认的数据源
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public bool IsDefault(string name)
    {
        return DefaultDataSource == name;
    }
    /// <summary>
    /// 检查虚拟数据源是否包含默认值
    /// </summary>
    public void CheckVirtualDataSource()
    {
        if (DefaultDataSource.IsNull())
        {
            throw new ShardingInvalidOperationException($"virtual data source not inited {nameof(DefaultDataSource)} in IShardingDbContext null");
        }

        if (DefaultConnection.IsNull())
        {
            throw new ShardingInvalidOperationException($"virtual data source not inited {nameof(DefaultConnection)} in IShardingDbContext null");
        }
    }

    public DbContextOptionsBuilder UseDbContextOptionsBuilder(string connectionString,
        DbContextOptionsBuilder dbContextOptionsBuilder)
    {
        var doUseDbContextOptionsBuilder = Options.UseDbContextOptionsBuilder(connectionString, dbContextOptionsBuilder);
        doUseDbContextOptionsBuilder.UseShardingInnerDb();
        Options.UseExecutorDbContextOptionBuilder(dbContextOptionsBuilder);
        return doUseDbContextOptionsBuilder;
    }

    public DbContextOptionsBuilder UseDbContextOptionsBuilder(DbConnection dbConnection,
        DbContextOptionsBuilder dbContextOptionsBuilder)
    {
        var doUseDbContextOptionsBuilder = Options.UseDbContextOptionsBuilder(dbConnection, dbContextOptionsBuilder);
        doUseDbContextOptionsBuilder.UseShardingInnerDb();
        Options.UseExecutorDbContextOptionBuilder(dbContextOptionsBuilder);
        return doUseDbContextOptionsBuilder;
    }

    public IReadOnlyDictionary<string, string> GetDataSource()
    {
        return _physicDataSourcePool.GetDataSource();
    }
}

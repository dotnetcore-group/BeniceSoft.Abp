using System.Collections.Concurrent;
using System.Data.Common;
using BeniceSoft.Core;
using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IVirtualDataSourceOptions
{
    /// <summary>
    /// 不能小于等于0
    /// </summary>
    int MaxQueryConnections { get; }

    /// <summary>
    /// 连接模式,如果没有什么特殊情况请是用系统自动 
    /// 默认<code>ShardingConnectionMode.Automatic</code>
    /// </summary>
    ConnectionMode ConnectionMode { get; }

    /// <summary>
    /// 默认数据源
    /// </summary>
    string DefaultDataSource { get; set; }

    /// <summary>
    /// 默认数据源链接字符串
    /// </summary>
    string DefaultConnection { get; set; }

    /// <summary>
    /// 额外数据源不能为null
    /// </summary>
    IDictionary<string, string> AdditionalDataSource { get; }

    /// <summary>
    /// 读写分离配置
    /// </summary>
    IDictionary<string, SeparationReadNode[]> Separation { get; }

    SeparationReadStrategy ReadStrategy { get; }

    SeparationBehavior SeparationBehavior { get; }

    int? SeparationPriority { get; }

    SeparationReadConnectionStrategy ReadConnectionStrategy { get; }

    /// <summary>
    /// 如何根据ConnectionString 配置 DbContextOptionsBuilder
    /// </summary>
    /// <param name="connectionString"></param>
    /// <param name="builder"></param>
    /// <returns></returns>
    DbContextOptionsBuilder UseDbContextOptionsBuilder(string connectionString, DbContextOptionsBuilder builder);

    /// <summary>
    /// 如何根据DbConnection 配置DbContextOptionsBuilder
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="builder"></param>
    /// <returns></returns>
    DbContextOptionsBuilder UseDbContextOptionsBuilder(DbConnection connection, DbContextOptionsBuilder builder);

    /// <summary>
    /// 外部DbContext
    /// </summary>
    /// <param name="builder"></param>
    void UseShellDbContextOptionBuilder(DbContextOptionsBuilder builder);

    /// <summary>
    /// 真实DbContextOptionBuilder的配置
    /// </summary>
    /// <param name="builder"></param>
    void UseExecutorDbContextOptionBuilder(DbContextOptionsBuilder builder);

    /// <summary>
    /// 使用读写分离
    /// </summary>
    /// <returns></returns>
    bool UseSeparation();
}

internal sealed class VirtualDataSourceOptions : IVirtualDataSourceOptions
{
    private readonly ShardingOptions _options;

    public VirtualDataSourceOptions(ShardingOptions options, IShardingProvider shardingProvider)
    {
        _options = options;
        MaxQueryConnections = options.MaxQueryConnections;
        ConnectionMode = options.ConnectionMode;
        DefaultDataSource = options.DefaultDataSource;
        DefaultConnection = options.DefaultConnection;
        AdditionalDataSource = options.AdditionalDataSourceFactory?.Invoke(shardingProvider) ?? new ConcurrentDictionary<string, string>();

        if (options.Separation != null)
        {
            if (options.Separation.SeparationNodeFactory != null)
            {
                var readNode = options.Separation.SeparationNodeFactory?.Invoke(shardingProvider);
                if (readNode != null)
                {
                    Separation = readNode.ToDictionary(d => d.Key, d => d.Value.ToArray());
                }
            }
            else
            {
                var node = options.Separation.SeparationFactory?.Invoke(shardingProvider);
                if (node != null)
                {
                    Separation = node.ToDictionary(d => d.Key, d => d.Value.Select(o => new SeparationReadNode(RandomUtils.GuidString(), o)).ToArray());
                }
            }

            ReadStrategy = options.Separation.ReadStrategy;
            SeparationBehavior = options.Separation.Behavior;
            SeparationPriority = options.Separation.DefaultPriority;
            ReadConnectionStrategy = options.Separation.ReadConnectionStrategy;
        }
    }

    public int MaxQueryConnections { get; } = Environment.ProcessorCount;

    public ConnectionMode ConnectionMode { get; } = ConnectionMode.Automatic;

    public string DefaultDataSource { get; set; }

    public string DefaultConnection { get; set; }

    public IDictionary<string, string> AdditionalDataSource { get; } = new Dictionary<string, string>();

    public IDictionary<string, SeparationReadNode[]> Separation { get; } = new Dictionary<string, SeparationReadNode[]>();

    public SeparationReadStrategy ReadStrategy { get; } = SeparationReadStrategy.Loop;

    public SeparationBehavior SeparationBehavior { get; } = SeparationBehavior.Disable;

    public int? SeparationPriority { get; }

    public SeparationReadConnectionStrategy ReadConnectionStrategy { get; } = SeparationReadConnectionStrategy.Cache;

    public bool UseSeparation()
    {
        return Separation.IsNotNull();
    }

    public DbContextOptionsBuilder UseDbContextOptionsBuilder(string connectionString, DbContextOptionsBuilder builder)
    {
        if (_options.ConnectionStringFactory == null)
        {
            throw new InvalidOperationException($"unknown {nameof(UseDbContextOptionsBuilder)} by connection string");
        }

        _options.ConnectionStringFactory.Invoke(connectionString, builder);
        return builder;
    }

    public DbContextOptionsBuilder UseDbContextOptionsBuilder(DbConnection connection, DbContextOptionsBuilder builder)
    {
        if (_options.ConnectionFactory == null)
        {
            throw new InvalidOperationException($"unknown {nameof(UseDbContextOptionsBuilder)} by connection");
        }

        _options.ConnectionFactory.Invoke(connection, builder);
        return builder;
    }

    public void UseExecutorDbContextOptionBuilder(DbContextOptionsBuilder builder)
    {
        _options.ShellDbContextFactory?.Invoke(builder);
    }

    public void UseShellDbContextOptionBuilder(DbContextOptionsBuilder builder)
    {
        _options.ExecutorDbContextFactory?.Invoke(builder);
    }
}

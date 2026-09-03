namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 读写分离配置
/// </summary>
public sealed class SeparationOptions
{
    public SeparationReadStrategy ReadStrategy { get; set; } = SeparationReadStrategy.Loop;

    public int DefaultPriority { get; set; } = 10;

    public SeparationReadConnectionStrategy ReadConnectionStrategy { get; set; } = SeparationReadConnectionStrategy.Cache;

    public SeparationBehavior Behavior { get; set; } = SeparationBehavior.Disable;

    public Func<IShardingProvider, IDictionary<string, IEnumerable<string>>>? SeparationFactory { get; set; }

    public Func<IShardingProvider, IDictionary<string, IEnumerable<SeparationReadNode>>>? SeparationNodeFactory { get; set; }
}

public sealed class SeparationReadNode
{
    public SeparationReadNode(string name, string connectionString)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        ConnectionString = connectionString;
    }

    /// <summary>
    /// 当前读库节点名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 当前读库链接的连接字符串
    /// </summary>
    public string ConnectionString { get; }
}

public sealed class SeparationContext
{
    private readonly Dictionary<string, string> _readNodes = [];

    public SeparationBehavior Behavior { get; set; } = SeparationBehavior.Disable;

    public int Priority { get; set; }

    public bool AddReadNode(string dataSource, string node)
    {
        if (_readNodes.ContainsKey(dataSource))
        {
            return false;
        }

        _readNodes.Add(dataSource, node);
        return true;
    }

    public bool TryGetReadNode(string dataSource, out string? node)
    {
        return _readNodes.TryGetValue(dataSource, out node);
    }
}

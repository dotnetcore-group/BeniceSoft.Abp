using System.Collections.Concurrent;
using BeniceSoft.Core;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 读写分离链接字符串解析
/// </summary>
internal interface ISeparationConnectionResolver
{
    bool Contains(string dataSource);

    /// <summary>
    /// 添加数据源从库读字符串
    /// </summary>
    /// <param name="dataSource"></param>
    /// <param name="readNode"></param>
    /// <param name="connectionString"></param>
    /// <returns></returns>
    bool AddConnectionString(string dataSource, string readNode, string connectionString);

    /// <summary>
    /// 获取指定数据源的读连接名称节点
    /// </summary>
    /// <param name="dataSource"></param>
    /// <param name="readNode">名称不存在报错,如果为null那么就随机获取</param>
    /// <returns></returns>
    string GetConnectionString(string dataSource, string? readNode = null);
}

internal sealed class SeparationConnectionResolver : ISeparationConnectionResolver
{
    private readonly SeparationReadStrategy _strategy;
    private readonly ConcurrentDictionary<string, ISeparationConnection> _connection = new();
    private readonly ISeparationConnectionFactory _factory;

    public SeparationConnectionResolver(SeparationReadStrategy strategy, ISeparationConnectionFactory factory, IEnumerable<ISeparationConnection> connections)
    {
        _strategy = strategy;
        _factory = factory;
        var enumerator = connections.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var currentConnector = enumerator.Current;
            if (currentConnector != null)
            {
                _connection.TryAdd(currentConnector.DataSource, currentConnector);
            }
        }
    }

    public bool AddConnectionString(string dataSource, string readNode, string connectionString)
    {
        if (!_connection.TryGetValue(dataSource, out var connector))
        {
            connector = _factory.Create(_strategy, dataSource,
                [
                    new(readNode ?? RandomUtils.GuidString(), connectionString)
                ]);
            return _connection.TryAdd(dataSource, connector);
        }
        else
        {
            return connector.AddConnectionString(connectionString, readNode);
        }
    }

    public bool Contains(string dataSource)
    {
        return _connection.ContainsKey(dataSource);
    }

    public string GetConnectionString(string dataSource, string? readNode = null)
    {
        if (!_connection.TryGetValue(dataSource, out var connector))
        {
            throw new ShardingInvalidOperationException($"read write connector not found, data source name:[{dataSource}]");
        }

        return connector.GetConnectionString(readNode);
    }
}

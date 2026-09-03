using BeniceSoft.Core;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface ISeparationConnection
{
    /// <summary>
    /// 数据源
    /// </summary>
    string DataSource { get; }

    /// <summary>
    /// 获取链接字符串
    /// </summary>
    /// <param name="readNode">可为null</param>
    /// <returns></returns>
    string GetConnectionString(string? readNode);

    /// <summary>
    /// 添加链接字符串
    /// </summary>
    /// <param name="readNode"></param>
    /// <param name="connectionString"></param>
    /// <returns></returns>
    bool AddConnectionString(string readNode, string connectionString);
}

internal abstract class SeparationConnection(string dataSource, SeparationReadNode[] nodes) : ISeparationConnection
{
    private readonly object _locker = new();
    private readonly List<SeparationReadNode> _nodes = [.. nodes];

    protected IReadOnlyList<SeparationReadNode> Nodes => _nodes;

    protected int Count => Nodes.Count;

    public string DataSource { get; } = dataSource;

    public bool AddConnectionString(string readNode, string connectionString)
    {
        lock (_locker)
        {
            _nodes.Add(new SeparationReadNode(readNode ?? RandomUtils.GuidString(), connectionString));
        }

        return true;
    }

    public abstract string GetConnectionString(string? readNode);
}

internal sealed class SeparationLoopConnection(string dataSource, SeparationReadNode[] nodes) : SeparationConnection(dataSource, nodes)
{
    private long _seed;

    public override string GetConnectionString(string? readNode)
    {
        if (readNode.IsNull())
        {
            if (Count == 1)
            {
                return Nodes[0].ConnectionString;
            }

            var newValue = Interlocked.Increment(ref _seed);
            var next = (int)(newValue % Count);
            if (next < 0)
            {
                return Nodes[Math.Abs(next)].ConnectionString;
            }

            return Nodes[next].ConnectionString;
        }

        return Nodes.FirstOrDefault(o => o.Name == readNode)?.ConnectionString ?? throw new ShardingInvalidOperationException($"read node name :[{readNode}] not found");
    }
}

internal sealed class SeparationRandomConnection(string dataSource, SeparationReadNode[] nodes) : SeparationConnection(dataSource, nodes)
{
    public override string GetConnectionString(string? readNode)
    {
        if (readNode.IsNull())
        {
            if (Count == 1)
            {
                return Nodes[0].ConnectionString;
            }

            var next = Random.Shared.Next(0, Count);
            return Nodes[next].ConnectionString;
        }

        return Nodes.FirstOrDefault(o => o.Name == readNode)?.ConnectionString ?? throw new ShardingInvalidOperationException($"read node name :[{readNode}] not found");
    }
}

using RabbitMQ.Client;
using Volo.Abp.RabbitMQ;

namespace BeniceSoft.Abp.Extensions.RabbitMQ;

/// <summary>
/// 基于 ABP <see cref="IConnectionPool"/> 的连接适配器
/// </summary>
public sealed class AbpConnectionPoolRabbitConnection : IRabbitConnection
{
    private readonly IConnectionPool _connectionPool;
    private readonly string? _connectionName;

    public AbpConnectionPoolRabbitConnection(IConnectionPool connectionPool, string? connectionName = null)
    {
        _connectionPool = connectionPool ?? throw new ArgumentNullException(nameof(connectionPool));
        _connectionName = connectionName;
    }

    /// <summary>
    /// 池内连接是否可用需异步探测；此处表示适配器本身可用，真正连通性在 CreateChannel 时校验。
    /// </summary>
    public bool IsConnected => true;

    public Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
        => CreateChannelAsync(null, cancellationToken);

    public async Task<IChannel> CreateChannelAsync(CreateChannelOptions? options, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionPool.GetAsync(_connectionName);
        if (!connection.IsOpen)
        {
            throw new InvalidOperationException(
                $"RabbitMQ connection '{_connectionName ?? "Default"}' from ABP ConnectionPool is not open");
        }

        return await connection.CreateChannelAsync(options, cancellationToken);
    }
}

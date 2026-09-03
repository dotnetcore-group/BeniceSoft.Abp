using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// Redis 连接管理
/// </summary>
public class RedisConnection : IRedisConnection
{
    private readonly object _locker = new();
    private readonly ConfigurationOptions _options;
    private readonly ILogger<RedisConnection>? _logger;
    private ConnectionMultiplexer? _connection;
    private bool _disposed;

    public RedisConnection(string connectionString, ILogger<RedisConnection>? logger = null)
        : this(GetOptions(connectionString), logger)
    {
    }

    public RedisConnection(ConfigurationOptions options, ILogger<RedisConnection>? logger = null)
    {
        _options = options;
        _logger = logger;
    }

    private static ConfigurationOptions GetOptions(string connectionString)
    {
        var options = ConfigurationOptions.Parse(connectionString);
        options.AllowAdmin = true;
        options.AbortOnConnectFail = false;
        options.ReconnectRetryPolicy = new ExponentialRetry(1000);
        return options;
    }

    public IConnectionMultiplexer TryConnect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection != null)
        {
            return _connection;
        }

        lock (_locker)
        {
            if (_connection != null)
            {
                return _connection;
            }

            var conn = ConnectionMultiplexer.Connect(_options);

            // 连接失败
            conn.ConnectionFailed += (sender, e) =>
                _logger?.LogWarning(e.Exception, "ConnectionFailed: {Host} ConnectionType: {Type} FailureType: {Failure}",
                    e.EndPoint?.GetFriendlyName(), e.ConnectionType, e.FailureType);

            // 重新建立连接
            conn.ConnectionRestored += (sender, e) =>
                _logger?.LogInformation("ConnectionRestored: {Host} ConnectionType: {Type} FailureType: {Failure}",
                    e.EndPoint?.GetFriendlyName(), e.ConnectionType, e.FailureType);

            // 发生内部错误
            conn.ErrorMessage += (sender, e) =>
                _logger?.LogWarning("ErrorMessage: {Host} Message: {Message}",
                    e.EndPoint?.GetFriendlyName(), e.Message);

            // 类库发生的错误
            conn.InternalError += (sender, e) =>
                _logger?.LogError(e.Exception, "InternalError: {Host}",
                    e.EndPoint?.GetFriendlyName());

            // 集群被修改
            conn.HashSlotMoved += (sender, e) =>
                _logger?.LogInformation("HashSlotMoved: New:{New} Old:{Old}",
                    e.NewEndPoint?.GetFriendlyName(), e.OldEndPoint?.GetFriendlyName());

            // 重新配置广播时（通常意味着主从同步更改）
            conn.ConfigurationChangedBroadcast += (sender, e) =>
                _logger?.LogInformation("ConfigurationChangedBroadcast: {Host}",
                    e.EndPoint?.GetFriendlyName());

            _connection = conn;
            return _connection;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_connection == null)
        {
            return;
        }

        _disposed = true;
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 监听频道消息（消息队列）
    /// </summary>
    public void Subscribe(string channel, Action<string, string> handler, bool pattern = false)
    {
        TryConnect().GetSubscriber().Subscribe(channel.CreateChannel(pattern), (c, m) => handler?.Invoke(c!, m!));
    }

    /// <summary>
    /// 监听频道消息（消息队列）
    /// </summary>
    public Task SubscribeAsync(string channel, Action<string, string> handler, bool pattern = false)
    {
        return TryConnect().GetSubscriber().SubscribeAsync(channel.CreateChannel(pattern), (c, m) => handler?.Invoke(c!, m!));
    }

    /// <summary>
    /// 取消监听指定频道
    /// </summary>
    public void Unsubscribe(string channel, Action<string, string>? handler = null, bool pattern = false)
    {
        TryConnect().GetSubscriber().Unsubscribe(channel.CreateChannel(pattern), (c, m) => handler?.Invoke(c!, m!));
    }

    /// <summary>
    /// 取消监听指定频道
    /// </summary>
    public Task UnsubscribeAsync(string channel, Action<string, string>? handler = null, bool pattern = false)
    {
        return TryConnect().GetSubscriber().UnsubscribeAsync(channel.CreateChannel(pattern), (c, m) => handler?.Invoke(c!, m!));
    }

    /// <summary>
    /// 取消监听所有频道
    /// </summary>
    public void UnsubscribeAll()
    {
        TryConnect().GetSubscriber().UnsubscribeAll();
    }

    /// <summary>
    /// 取消监听所有频道
    /// </summary>
    public Task UnsubscribeAllAsync()
    {
        return TryConnect().GetSubscriber().UnsubscribeAllAsync();
    }

    /// <summary>
    /// 发布监听消息
    /// </summary>
    public long Publish(string channel, string message, bool pattern = false)
    {
        return TryConnect().GetSubscriber().Publish(channel.CreateChannel(pattern), message);
    }

    /// <summary>
    /// 发布消息
    /// </summary>
    public Task<long> PublishAsync(string channel, string message, bool pattern = false)
    {
        return TryConnect().GetSubscriber().PublishAsync(channel.CreateChannel(pattern), message);
    }
}


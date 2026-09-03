using StackExchange.Redis;

namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// Redis 连接接口
/// </summary>
public interface IRedisConnection : IDisposable
{
    /// <summary>
    /// 尝试连接 Redis
    /// </summary>
    IConnectionMultiplexer TryConnect();

    /// <summary>
    /// 监听频道消息（消息队列）
    /// </summary>
    void Subscribe(string channel, Action<string, string> handler, bool pattern = false);

    /// <summary>
    /// 监听频道消息（消息队列）
    /// </summary>
    Task SubscribeAsync(string channel, Action<string, string> handler, bool pattern = false);

    /// <summary>
    /// 取消监听指定频道
    /// </summary>
    void Unsubscribe(string channel, Action<string, string>? handler = null, bool pattern = false);

    /// <summary>
    /// 取消监听指定频道
    /// </summary>
    Task UnsubscribeAsync(string channel, Action<string, string>? handler = null, bool pattern = false);

    /// <summary>
    /// 取消监听所有频道
    /// </summary>
    void UnsubscribeAll();

    /// <summary>
    /// 取消监听所有频道
    /// </summary>
    Task UnsubscribeAllAsync();

    /// <summary>
    /// 发布监听消息
    /// </summary>
    long Publish(string channel, string message, bool pattern = false);

    /// <summary>
    /// 发布监听消息
    /// </summary>
    Task<long> PublishAsync(string channel, string message, bool pattern = false);
}


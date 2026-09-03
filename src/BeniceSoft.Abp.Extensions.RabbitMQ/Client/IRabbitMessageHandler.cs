using RabbitMQ.Client;

namespace BeniceSoft.Abp.Extensions.RabbitMQ;

public interface IRabbitMessageHandler<in TMessage>
{
    Task<RabbitMessageResult> HandleAsync(TMessage message, IReadOnlyBasicProperties properties, CancellationToken cancellationToken = default);
}

public enum RabbitMessageResult
{
    /// <summary>
    /// 处理成功，ACK 并丢弃
    /// </summary>
    Success = 0,

    /// <summary>
    /// 重新入队（可能导致死循环，优先用 Redelivery）
    /// </summary>
    Requeue = 1,

    /// <summary>
    /// 重新投递到队列末端
    /// </summary>
    Redelivery = 2,

    /// <summary>
    /// 拒绝并不再入队（可进死信）
    /// </summary>
    DeadLetter = 3
}

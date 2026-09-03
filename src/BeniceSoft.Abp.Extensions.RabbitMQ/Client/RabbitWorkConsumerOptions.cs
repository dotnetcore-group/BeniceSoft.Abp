namespace BeniceSoft.Abp.Extensions.RabbitMQ;

public class RabbitWorkConsumerOptions
{
    /// <summary>工作队列名（平台在代码中指定，不进配置文件）。</summary>
    public string QueueName { get; set; } = string.Empty;

    /// <summary>
    /// 通道未 ACK 在途消息上限。不是「一次取多少条消息合并处理」。
    /// Prefetch&gt;1 时同一 Handler 可能并发处理多条消息。
    /// </summary>
    public ushort PrefetchCount { get; set; } = 1;

    /// <summary>Redelivery 最大次数；&lt;=0 表示不限制。</summary>
    public int MaxRedeliveryTimes { get; set; } = -1;

    /// <summary>
    /// ABP RabbitMQ:Connections 中的连接名，默认 Default。
    /// </summary>
    public string? ConnectionName { get; set; }

    public RabbitRelational? Relational { get; set; }
}

public sealed class RabbitConsumerRegistration
{
    public required Type MessageType { get; init; }

    public required Type HandlerType { get; init; }

    public required RabbitWorkConsumerOptions Options { get; init; }
}

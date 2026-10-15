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

    /// <summary>
    /// Max retries for unhandled handler exceptions. The message is republished to the queue with the
    /// retry header; once exhausted, <see cref="RetryExhaustedAction"/> applies.
    /// Default 3. A negative value restores the legacy behavior (BasicReject requeue, unbounded).
    /// </summary>
    public int MaxExceptionRetryTimes { get; set; } = 3;

    /// <summary>
    /// What happens when exception retries or <see cref="MaxRedeliveryTimes"/> are exhausted.
    /// </summary>
    public RabbitRetryExhaustedAction RetryExhaustedAction { get; set; } = RabbitRetryExhaustedAction.Auto;

    /// <summary>
    /// Skip messages whose MessageId was already processed by this consumer.
    /// Requires an <see cref="IRabbitInboxStore"/> (e.g. AddBeniceSoftRabbitMqEfCore&lt;TDbContext&gt;()).
    /// </summary>
    public bool Idempotent { get; set; }

    /// <summary>Inbox consumer name; defaults to the handler type full name. Keep it stable.</summary>
    public string? ConsumerName { get; set; }
}

public enum RabbitRetryExhaustedAction
{
    /// <summary>Dead-letter when the queue declares x-dead-letter-exchange; otherwise log an error and ACK (drop).</summary>
    Auto = 0,

    /// <summary>Always BasicReject(requeue:false); dropped by the broker if no DLX is configured.</summary>
    DeadLetter = 1,

    /// <summary>Log an error and ACK (legacy MaxRedeliveryTimes behavior).</summary>
    Drop = 2
}

public sealed class RabbitConsumerRegistration
{
    public required Type MessageType { get; init; }

    public required Type HandlerType { get; init; }

    public required RabbitWorkConsumerOptions Options { get; init; }
}

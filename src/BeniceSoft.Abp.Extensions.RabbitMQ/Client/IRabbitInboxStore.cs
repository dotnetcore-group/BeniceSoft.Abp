namespace BeniceSoft.Abp.Extensions.RabbitMQ;

/// <summary>
/// Inbox used by idempotent consumers (<see cref="RabbitWorkConsumerOptions.Idempotent"/>).
/// Implementations must run <c>handler</c> and record (MessageId, ConsumerName) atomically,
/// and skip the handler when the pair was already recorded.
/// See BeniceSoft.Abp.Extensions.RabbitMQ.EntityFrameworkCore for the EF Core implementation.
/// </summary>
public interface IRabbitInboxStore
{
    Task<RabbitMessageResult> ProcessOnceAsync(
        string messageId,
        string consumerName,
        Func<Task<RabbitMessageResult>> handler,
        CancellationToken cancellationToken = default);
}
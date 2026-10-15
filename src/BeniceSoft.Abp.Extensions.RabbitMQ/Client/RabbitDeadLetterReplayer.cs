using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using System.Collections;
using System.Text;
using Volo.Abp.RabbitMQ;

namespace BeniceSoft.Abp.Extensions.RabbitMQ;

/// <summary>
/// Moves messages from a dead-letter queue back to a work queue (ops replay).
/// </summary>
public interface IRabbitDeadLetterReplayer
{
    /// <summary>
    /// Replays up to <paramref name="count"/> messages from <paramref name="deadLetterQueue"/>.
    /// When <paramref name="targetQueue"/> is null the original queue is taken from the <c>x-death</c> header.
    /// The retry counter header is reset so the replayed message gets a full retry budget.
    /// </summary>
    /// <returns>Number of messages replayed.</returns>
    Task<int> ReplayAsync(
        string deadLetterQueue,
        int count,
        string? targetQueue = null,
        string? connectionName = null,
        CancellationToken cancellationToken = default);
}

public class RabbitDeadLetterReplayer : IRabbitDeadLetterReplayer
{
    private readonly IRabbitConnection _defaultConnection;
    private readonly IConnectionPool? _connectionPool;
    private readonly ILogger _logger;

    public RabbitDeadLetterReplayer(
        IRabbitConnection defaultConnection,
        IConnectionPool? connectionPool = null,
        ILogger<RabbitDeadLetterReplayer>? logger = null)
    {
        _defaultConnection = defaultConnection;
        _connectionPool = connectionPool;
        _logger = logger ?? NullLogger<RabbitDeadLetterReplayer>.Instance;
    }

    public async Task<int> ReplayAsync(
        string deadLetterQueue,
        int count,
        string? targetQueue = null,
        string? connectionName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deadLetterQueue);
        if (count <= 0)
        {
            return 0;
        }

        var connection = string.IsNullOrWhiteSpace(connectionName) || _connectionPool == null
            ? _defaultConnection
            : new AbpConnectionPoolRabbitConnection(_connectionPool, connectionName);

        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        var checkedQueues = new HashSet<string>();
        var replayed = 0;
        while (replayed < count)
        {
            var result = await channel.BasicGetAsync(deadLetterQueue, false, cancellationToken);
            if (result == null)
            {
                break;
            }

            var target = targetQueue ?? ReadOriginalQueue(result.BasicProperties.Headers);
            if (string.IsNullOrWhiteSpace(target))
            {
                await channel.BasicNackAsync(result.DeliveryTag, false, true, cancellationToken);
                throw new InvalidOperationException(
                    $"Cannot resolve original queue of message {result.BasicProperties.MessageId} in {deadLetterQueue}; pass targetQueue explicitly");
            }

            if (checkedQueues.Add(target))
            {
                // Throws (404) if the target queue does not exist, instead of silently dropping the message.
                await channel.QueueDeclarePassiveAsync(target, cancellationToken);
            }

            var properties = new BasicProperties(result.BasicProperties);
            properties.Headers?.Remove(RabbitSubscriber.ConsumptionFrequencyHeader);

            // Publisher confirms: awaits broker ack before removing the message from the DLQ.
            await channel.BasicPublishAsync(string.Empty, target, false, properties, result.Body, cancellationToken);
            await channel.BasicAckAsync(result.DeliveryTag, false, cancellationToken);
            replayed++;
        }

        _logger.LogInformation(
            "Replayed {Count} message(s) from dead-letter queue {Queue} to {Target}",
            replayed,
            deadLetterQueue,
            targetQueue ?? "<x-death queue>");
        return replayed;
    }

    private static string? ReadOriginalQueue(IDictionary<string, object?>? headers)
    {
        if (headers == null || !headers.TryGetValue("x-death", out var raw) || raw is not IList { Count: > 0 } deaths)
        {
            return null;
        }

        if (deaths[0] is IDictionary<string, object?> first && first.TryGetValue("queue", out var queue))
        {
            return queue switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                _ => queue?.ToString()
            };
        }

        return null;
    }
}
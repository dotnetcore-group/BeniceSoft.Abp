using System.Text;
using BeniceSoft.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BeniceSoft.Abp.Extensions.RabbitMQ;

/// <summary>
/// PrefetchCount（Qos）控制未 ACK 在途消息数；每条消息仍单独调用一次 Handler。
/// 异步消费者在 Prefetch&gt;1 时可能并发执行同一 Handler，需保证 Handler 线程安全。
/// </summary>
public class RabbitSubscriber : IAsyncDisposable
{
    public const string ConsumptionFrequencyHeader = "BeniceSoft.RabbitSubscriber.ConsumptionFrequency";

    private readonly IRabbitConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _queueName;
    private readonly RabbitRelational? _relational;
    private readonly ushort _prefetchCount;
    private readonly int _maxRedeliveryTimes;
    private readonly Type _messageType;
    private readonly Type _handlerType;
    private readonly ILogger _logger;
    private readonly RabbitMessageContextPropagator? _messageContextPropagator;
    private string? _consumerTag;

    public RabbitSubscriber(
        IRabbitConnection connection,
        IServiceScopeFactory scopeFactory,
        RabbitRelational relational,
        Type messageType,
        Type handlerType,
        ushort prefetchCount = 1,
        int maxRedeliveryTimes = -1,
        ILogger? logger = null,
        RabbitMessageContextPropagator? messageContextPropagator = null)
        : this(
            connection,
            scopeFactory,
            relational.QueueDeclare?.Name ?? throw new ArgumentException("QueueDeclare.Name is required"),
            messageType,
            handlerType,
            prefetchCount,
            maxRedeliveryTimes,
            logger,
            messageContextPropagator)
    {
        _relational = relational;
    }

    public RabbitSubscriber(
        IRabbitConnection connection,
        IServiceScopeFactory scopeFactory,
        string queueName,
        Type messageType,
        Type handlerType,
        ushort prefetchCount = 1,
        int maxRedeliveryTimes = -1,
        ILogger? logger = null,
        RabbitMessageContextPropagator? messageContextPropagator = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(handlerType);

        _connection = connection;
        _scopeFactory = scopeFactory;
        _queueName = queueName;
        _messageType = messageType;
        _handlerType = handlerType;
        _prefetchCount = prefetchCount == 0 ? (ushort)1 : prefetchCount;
        _maxRedeliveryTimes = maxRedeliveryTimes;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _messageContextPropagator = messageContextPropagator;
    }

    public IChannel? Channel { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Channel != null)
        {
            throw new InvalidOperationException("this consumer has already subscribed");
        }

        Channel = await _connection.CreateChannelAsync(cancellationToken);
        if (_relational == null)
        {
            await Channel.QueueDeclareAsync(_queueName, true, false, false, null, false, cancellationToken);
        }
        else
        {
            await _relational.ConsumerDeclareAsync(Channel, cancellationToken);
        }

        await Channel.BasicQosAsync(0, _prefetchCount, false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(Channel);
        consumer.ReceivedAsync += OnReceivedAsync;
        _consumerTag = await Channel.BasicConsumeAsync(_queueName, false, consumer, cancellationToken);
        _logger.LogInformation(
            "RabbitMQ consumer started. Queue={Queue} Prefetch={Prefetch} Handler={Handler}",
            _queueName,
            _prefetchCount,
            _handlerType.Name);
    }

    /// <summary>Max retries for unhandled handler exceptions; &lt;0 = legacy unbounded requeue.</summary>
    public int MaxExceptionRetryTimes { get; set; } = 3;

    /// <summary>Action when retries are exhausted.</summary>
    public RabbitRetryExhaustedAction RetryExhaustedAction { get; set; } = RabbitRetryExhaustedAction.Auto;

    /// <summary>Non-empty enables idempotent consumption via <see cref="IRabbitInboxStore"/>.</summary>
    public string? InboxConsumerName { get; set; }

    private bool HasDeadLetterExchange =>
        _relational?.QueueDeclare?.Arguments?.ContainsKey("x-dead-letter-exchange") == true;

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs e)
    {
        var messageText = Encoding.UTF8.GetString(e.Body.ToArray());
        RabbitMessageResult result;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            using var userContext = _messageContextPropagator?.Restore(scope.ServiceProvider, e.BasicProperties);
            result = await HandleAsync(scope.ServiceProvider, e, messageText);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing rabbit message on queue {Queue}, MessageId={MessageId}: {Msg}",
                _queueName,
                e.BasicProperties.MessageId,
                messageText);
            await SettleAsync(() => OnExceptionAsync(e));
            return;
        }

        await SettleAsync(() => ApplyResultAsync(e, result));
    }

    private async Task<RabbitMessageResult> HandleAsync(IServiceProvider serviceProvider, BasicDeliverEventArgs e, string messageText)
    {
        var handler = serviceProvider.GetRequiredService(_handlerType);
        var message = JsonUtils.Deserialize(messageText, _messageType, JsonUtils.DefaultOptions);
        if (message == null)
        {
            throw new InvalidOperationException($"Failed to deserialize message for queue {_queueName}");
        }

        var handleMethod = typeof(IRabbitMessageHandler<>)
            .MakeGenericType(_messageType)
            .GetMethod(nameof(IRabbitMessageHandler<object>.HandleAsync))!;

        Task<RabbitMessageResult> InvokeAsync() => (Task<RabbitMessageResult>)handleMethod.Invoke(
            handler,
            [message, e.BasicProperties, CancellationToken.None])!;

        if (InboxConsumerName.IsNull())
        {
            return await InvokeAsync();
        }

        var messageId = e.BasicProperties.MessageId;
        if (messageId.IsNull())
        {
            _logger.LogWarning(
                "Idempotent consumer {Consumer} received a message without MessageId on queue {Queue}; processing without dedup",
                InboxConsumerName,
                _queueName);
            return await InvokeAsync();
        }

        var inbox = serviceProvider.GetService<IRabbitInboxStore>()
                    ?? throw new InvalidOperationException(
                        "Idempotent consumers require an IRabbitInboxStore, e.g. services.AddBeniceSoftRabbitMqEfCore<TDbContext>()");
        return await inbox.ProcessOnceAsync(messageId!, InboxConsumerName!, InvokeAsync);
    }

    private async Task SettleAsync(Func<Task> settle)
    {
        try
        {
            await settle();
        }
        catch (Exception ex)
        {
            // Channel failure: the broker will redeliver the unacked message.
            _logger.LogError(ex, "Failed to settle rabbit message on queue {Queue}", _queueName);
        }
    }

    private async Task OnExceptionAsync(BasicDeliverEventArgs e)
    {
        if (Channel == null)
        {
            return;
        }

        if (MaxExceptionRetryTimes < 0)
        {
            await Channel.BasicRejectAsync(e.DeliveryTag, true);
            return;
        }

        await RetryOrExhaustAsync(e, MaxExceptionRetryTimes + 1);
    }

    private async Task ApplyResultAsync(BasicDeliverEventArgs e, RabbitMessageResult result)
    {
        if (Channel == null)
        {
            return;
        }

        switch (result)
        {
            case RabbitMessageResult.DeadLetter:
                await Channel.BasicRejectAsync(e.DeliveryTag, false);
                break;
            case RabbitMessageResult.Requeue:
                await Channel.BasicRejectAsync(e.DeliveryTag, true);
                break;
            case RabbitMessageResult.Redelivery when _maxRedeliveryTimes > 0:
                await RetryOrExhaustAsync(e, _maxRedeliveryTimes);
                break;
            case RabbitMessageResult.Redelivery:
                await Channel.BasicPublishAsync(e.Exchange, e.RoutingKey, false, new BasicProperties(e.BasicProperties), e.Body);
                await Channel.BasicAckAsync(e.DeliveryTag, false);
                break;
            default:
                await Channel.BasicAckAsync(e.DeliveryTag, false);
                break;
        }
    }

    /// <summary>
    /// <paramref name="maxAttempts"/> = total deliveries allowed (same semantics as MaxRedeliveryTimes).
    /// Retries are republished to this queue only (default exchange), so fanout/topic peers are not duplicated.
    /// </summary>
    private async Task RetryOrExhaustAsync(BasicDeliverEventArgs e, int maxAttempts)
    {
        var frequency = GetConsumptionFrequency(e.BasicProperties) + 1;
        if (frequency < maxAttempts)
        {
            var properties = new BasicProperties(e.BasicProperties);
            SetConsumptionFrequency(properties, frequency);
            await Channel!.BasicPublishAsync(string.Empty, _queueName, false, properties, e.Body);
            await Channel.BasicAckAsync(e.DeliveryTag, false);
            return;
        }

        var deadLetter = RetryExhaustedAction switch
        {
            RabbitRetryExhaustedAction.DeadLetter => true,
            RabbitRetryExhaustedAction.Drop => false,
            _ => HasDeadLetterExchange
        };

        if (deadLetter)
        {
            _logger.LogWarning(
                "Retries exhausted ({Attempts}), dead-lettering message {MessageId} from queue {Queue}",
                frequency,
                e.BasicProperties.MessageId,
                _queueName);
            await Channel!.BasicRejectAsync(e.DeliveryTag, false);
        }
        else
        {
            _logger.LogError(
                "Retries exhausted ({Attempts}) and no dead-letter exchange; dropping message {MessageId} from queue {Queue}",
                frequency,
                e.BasicProperties.MessageId,
                _queueName);
            await Channel!.BasicAckAsync(e.DeliveryTag, false);
        }
    }

    private static int GetConsumptionFrequency(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers == null)
        {
            return 0;
        }

        return properties.Headers.TryGetValue(ConsumptionFrequencyHeader, out var frequency)
            ? frequency.ToStringSafe().ToInt32()
            : 0;
    }

    private static void SetConsumptionFrequency(IBasicProperties properties, int frequency)
    {
        properties.Headers ??= new Dictionary<string, object?>();
        properties.Headers[ConsumptionFrequencyHeader] = frequency;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_consumerTag.IsNull() && Channel != null)
        {
            try
            {
                await Channel.BasicCancelAsync(_consumerTag!);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cancel consumer {Tag}", _consumerTag);
            }
        }

        Channel?.Dispose();
        GC.SuppressFinalize(this);
    }
}

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

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs e)
    {
        var messageText = Encoding.UTF8.GetString(e.Body.ToArray());
        try
        {
            using var scope = _scopeFactory.CreateScope();
            using var userContext = _messageContextPropagator?.Restore(scope.ServiceProvider, e.BasicProperties);
            var handler = scope.ServiceProvider.GetRequiredService(_handlerType);
            var message = JsonUtils.Deserialize(messageText, _messageType, JsonUtils.DefaultOptions);
            if (message == null)
            {
                throw new InvalidOperationException($"Failed to deserialize message for queue {_queueName}");
            }

            var handleMethod = typeof(IRabbitMessageHandler<>)
                .MakeGenericType(_messageType)
                .GetMethod(nameof(IRabbitMessageHandler<object>.HandleAsync))!;

            var resultTask = (Task<RabbitMessageResult>)handleMethod.Invoke(
                handler,
                [message, e.BasicProperties, CancellationToken.None])!;

            var result = await resultTask;
            await ApplyResultAsync(e, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing rabbit message on queue {Queue}: {Msg}", _queueName, messageText);
            if (Channel != null)
            {
                await Channel.BasicRejectAsync(e.DeliveryTag, true);
            }
        }
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
            case RabbitMessageResult.Success:
            case RabbitMessageResult.Redelivery:
                if (result == RabbitMessageResult.Redelivery)
                {
                    var redelivery = true;
                    var properties = new BasicProperties(e.BasicProperties);
                    if (_maxRedeliveryTimes > 0)
                    {
                        var frequency = GetConsumptionFrequency(e.BasicProperties) + 1;
                        if (frequency < _maxRedeliveryTimes)
                        {
                            SetConsumptionFrequency(properties, frequency);
                        }
                        else
                        {
                            redelivery = false;
                        }
                    }

                    if (redelivery)
                    {
                        await Channel.BasicPublishAsync(e.Exchange, e.RoutingKey, false, properties, e.Body);
                    }
                }

                await Channel.BasicAckAsync(e.DeliveryTag, false);
                break;
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

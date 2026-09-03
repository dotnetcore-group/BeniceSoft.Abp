using BeniceSoft.Core;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace BeniceSoft.Abp.Extensions.RabbitMQ;

public interface IRabbitPublisher : IDisposable
{
    Task PublishAsync<T>(T data, Action<BasicProperties>? setup = null, CancellationToken cancellationToken = default);

    Task PublishAsync<T>(
        T data,
        bool propagateUserContext,
        Action<BasicProperties>? setup = null,
        CancellationToken cancellationToken = default);
}

public class RabbitPublisher : IRabbitPublisher
{
    private readonly IRabbitConnection _connection;
    private readonly string _exchange;
    private readonly string _routingKey;
    private readonly RabbitRelational? _relational;
    private readonly ILogger _logger;
    private readonly RabbitMessageContextPropagator? _messageContextPropagator;
    private IChannel? _channel;

    public RabbitPublisher(
        IRabbitConnection connection,
        string exchange,
        string routingKey,
        ILogger<RabbitPublisher>? logger = null,
        RabbitMessageContextPropagator? messageContextPropagator = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _exchange = exchange ?? string.Empty;
        _routingKey = routingKey ?? string.Empty;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RabbitPublisher>.Instance;
        _messageContextPropagator = messageContextPropagator;
    }

    public RabbitPublisher(
        IRabbitConnection connection,
        RabbitRelational relational,
        ILogger<RabbitPublisher>? logger = null,
        RabbitMessageContextPropagator? messageContextPropagator = null)
        : this(connection, relational.ExchangeDeclare?.Name ?? string.Empty, relational.RoutingKey, logger, messageContextPropagator)
    {
        _relational = relational;
    }

    public Task PublishAsync<T>(T data, Action<BasicProperties>? setup = null, CancellationToken cancellationToken = default)
        => PublishAsync(data, propagateUserContext: true, setup, cancellationToken);

    public async Task PublishAsync<T>(
        T data,
        bool propagateUserContext,
        Action<BasicProperties>? setup = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        await EnsureChannelAsync(cancellationToken);

        var properties = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent
        };
        setup?.Invoke(properties);

        if (propagateUserContext)
        {
            _messageContextPropagator?.Attach(properties);
        }

        var body = JsonUtils.SerializeBytes(data, JsonUtils.DefaultOptions);
        await _channel!.BasicPublishAsync(_exchange, _routingKey, false, properties, body, cancellationToken);
    }

    private async Task EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return;
        }

        _channel?.Dispose();
        _channel = await _connection.CreateChannelAsync(cancellationToken);
        if (_relational != null)
        {
            await _relational.ProducerDeclareAsync(_channel, cancellationToken);
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        GC.SuppressFinalize(this);
    }
}

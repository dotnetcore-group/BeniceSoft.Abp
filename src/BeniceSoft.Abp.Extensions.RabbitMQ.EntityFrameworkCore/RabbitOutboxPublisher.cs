using BeniceSoft.Core;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.Extensions.RabbitMQ.EntityFrameworkCore;

/// <summary>
/// Writes messages to the outbox table inside the current unit of work, on the same connection/transaction
/// as the business DbContext. Nothing reaches RabbitMQ unless the UoW commits.
/// </summary>
public interface IRabbitOutboxPublisher
{
    /// <returns>The MessageId (also the consumer idempotency key).</returns>
    Task<string> PublishAsync<T>(
        RabbitRelational relational,
        T data,
        Action<BasicProperties>? setup = null,
        string? connectionName = null,
        CancellationToken cancellationToken = default);

    Task<string> PublishToQueueAsync<T>(
        string queueName,
        T data,
        Action<BasicProperties>? setup = null,
        string? connectionName = null,
        CancellationToken cancellationToken = default);
}

public class RabbitOutboxPublisher : IRabbitOutboxPublisher
{
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly RabbitMessagingDbContextResolver _dbContextResolver;
    private readonly RabbitMessagingTableInitializer _tableInitializer;
    private readonly RabbitOutboxSignal _signal;
    private readonly RabbitOutboxOptions _options;
    private readonly RabbitMessageContextPropagator? _messageContextPropagator;

    public RabbitOutboxPublisher(
        IUnitOfWorkManager unitOfWorkManager,
        RabbitMessagingDbContextResolver dbContextResolver,
        RabbitMessagingTableInitializer tableInitializer,
        RabbitOutboxSignal signal,
        IOptions<RabbitOutboxOptions> options,
        RabbitMessageContextPropagator? messageContextPropagator = null)
    {
        _unitOfWorkManager = unitOfWorkManager;
        _dbContextResolver = dbContextResolver;
        _tableInitializer = tableInitializer;
        _signal = signal;
        _options = options.Value;
        _messageContextPropagator = messageContextPropagator;
    }

    public Task<string> PublishToQueueAsync<T>(
        string queueName,
        T data,
        Action<BasicProperties>? setup = null,
        string? connectionName = null,
        CancellationToken cancellationToken = default)
        => PublishAsync(RabbitRelational.Work(queueName), data, setup, connectionName, cancellationToken);

    public async Task<string> PublishAsync<T>(
        RabbitRelational relational,
        T data,
        Action<BasicProperties>? setup = null,
        string? connectionName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relational);
        ArgumentNullException.ThrowIfNull(data);

        var unitOfWork = _unitOfWorkManager.Current
                         ?? throw new InvalidOperationException(
                             "IRabbitOutboxPublisher must be called inside a unit of work (e.g. an application service method or IUnitOfWorkManager.Begin)");

        var dbContext = await _dbContextResolver.ResolveAsync(preferActive: true)
                        ?? throw new InvalidOperationException(
                            "No EF Core DbContext for the outbox: touch a repository/DbContext in this unit of work first, or set RabbitOutboxOptions.DbContextType");

        await _tableInitializer.EnsureTablesAsync(dbContext, cancellationToken);

        var properties = new BasicProperties();
        setup?.Invoke(properties);
        _messageContextPropagator?.Attach(properties);
        if (properties.MessageId.IsNull())
        {
            properties.MessageId = Guid.NewGuid().ToString("N");
        }

        var now = DateTime.UtcNow;
        var db = await RabbitMessagingDb.FromAsync(dbContext, cancellationToken);
        await db.ExecuteAsync(
            $@"INSERT INTO {_options.OutboxTable}
(id, message_id, exchange_name, routing_key, connection_name, body, headers, correlation_id, message_type, status, retry_count, creation_time, next_retry_time)
VALUES (@id, @messageId, @exchange, @routingKey, @connectionName, @body, @headers, @correlationId, @messageType, 0, 0, @now, @now)",
            new
            {
                id = Guid.NewGuid().ToString(),
                messageId = properties.MessageId,
                exchange = relational.ExchangeDeclare?.Name ?? string.Empty,
                routingKey = relational.RoutingKey,
                connectionName,
                body = JsonUtils.SerializeBytes(data, JsonUtils.DefaultOptions),
                headers = RabbitOutboxHeaders.Serialize(properties.Headers),
                correlationId = properties.CorrelationId,
                messageType = properties.Type,
                now
            },
            cancellationToken);

        unitOfWork.OnCompleted(() =>
        {
            _signal.Notify();
            return Task.CompletedTask;
        });

        return properties.MessageId!;
    }
}

internal static class RabbitOutboxHeaders
{
    public static string? Serialize(IDictionary<string, object?>? headers)
    {
        if (headers == null || headers.Count == 0)
        {
            return null;
        }

        var map = headers.ToDictionary(
            x => x.Key,
            x => x.Value switch
            {
                null => null,
                byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
                _ => x.Value.ToString()
            });
        return JsonUtils.Serialize(map, JsonUtils.DefaultOptions);
    }

    public static IDictionary<string, object?>? Deserialize(string? json)
    {
        if (json.IsNull())
        {
            return null;
        }

        var map = JsonUtils.Deserialize<Dictionary<string, string?>>(json!, JsonUtils.DefaultOptions);
        return map?.ToDictionary(x => x.Key, x => (object?)x.Value);
    }
}
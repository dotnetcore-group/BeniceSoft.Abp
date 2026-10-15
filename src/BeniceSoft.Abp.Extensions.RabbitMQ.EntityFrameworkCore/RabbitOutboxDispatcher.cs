using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Volo.Abp.RabbitMQ;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.Extensions.RabbitMQ.EntityFrameworkCore;

/// <summary>
/// Creates the tables on startup, sends pending outbox rows (publisher confirms), marks them sent, retries with
/// backoff, and cleans up old rows. Rows are claimed with a lock token + lease via a conditional UPDATE,
/// so multiple instances never send the same row concurrently.
/// </summary>
public class RabbitOutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitOutboxSignal _signal;
    private readonly RabbitOutboxOptions _options;
    private readonly ILogger _logger;
    private DateTime _nextCleanupTime = DateTime.MinValue;

    public RabbitOutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        RabbitOutboxSignal signal,
        IOptions<RabbitOutboxOptions> options,
        ILogger<RabbitOutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _signal = signal;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.DispatcherEnabled)
        {
            return;
        }

        if (_options.DbContextType == null)
        {
            _logger.LogWarning(
                "RabbitMQ outbox dispatcher disabled: several (or no) DbContexts registered; set RabbitOutboxOptions.DbContextType");
            return;
        }

        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            var claimed = 0;
            try
            {
                claimed = await DispatchOnceAsync(stoppingToken);
                if (DateTime.UtcNow >= _nextCleanupTime)
                {
                    await CleanupAsync(stoppingToken);
                    _nextCleanupTime = DateTime.UtcNow + _options.CleanupInterval;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ outbox dispatch failed");
            }

            if (claimed >= _options.BatchSize)
            {
                continue;
            }

            try
            {
                await _signal.WaitAsync(_options.PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Ensures tables, claims and sends one batch. Returns the number of rows claimed.</summary>
    public Task<int> DispatchOnceAsync(CancellationToken cancellationToken = default)
        => WithDbAsync(async (db, scopeServices) =>
        {
            var rows = await ClaimAsync(db, cancellationToken);
            foreach (var group in rows.GroupBy(x => x.ConnectionName ?? string.Empty))
            {
                await SendGroupAsync(scopeServices, db, group.Key, group.ToList(), cancellationToken);
            }

            return rows.Count;
        }, cancellationToken);

    /// <summary>Deletes sent outbox rows and inbox rows older than their retention.</summary>
    public Task CleanupAsync(CancellationToken cancellationToken = default)
        => WithDbAsync(async (db, _) =>
        {
            await db.ExecuteAsync(
                $"DELETE FROM {_options.OutboxTable} WHERE status = 1 AND sent_time < @before",
                new { before = DateTime.UtcNow - _options.SentRetention },
                cancellationToken);
            await db.ExecuteAsync(
                $"DELETE FROM {_options.InboxTable} WHERE processed_time < @before",
                new { before = DateTime.UtcNow - _options.InboxRetention },
                cancellationToken);
            return 0;
        }, cancellationToken);

    private async Task<int> WithDbAsync(
        Func<RabbitMessagingDb, IServiceProvider, Task<int>> action,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        using var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>()
            .Begin(requiresNew: true, isTransactional: false);
        var dbContext = await scope.ServiceProvider.GetRequiredService<RabbitMessagingDbContextResolver>().GetConfiguredAsync();
        if (dbContext == null)
        {
            return 0;
        }

        await scope.ServiceProvider.GetRequiredService<RabbitMessagingTableInitializer>().EnsureTablesAsync(dbContext, cancellationToken);
        var result = await action(await RabbitMessagingDb.FromAsync(dbContext, cancellationToken), scope.ServiceProvider);
        await unitOfWork.CompleteAsync(cancellationToken);
        return result;
    }

    private async Task<List<OutboxRow>> ClaimAsync(RabbitMessagingDb db, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var ids = await db.QueryAsync<string>(
            db.Dialect.ClaimCandidatesSql(_options.OutboxTable),
            new { now, take = _options.BatchSize },
            cancellationToken);
        if (ids.Count == 0)
        {
            return [];
        }

        // Conditional UPDATE: only one instance wins each row.
        var token = Guid.NewGuid().ToString();
        await db.ExecuteAsync(
            $@"UPDATE {_options.OutboxTable} SET lock_token = @token, locked_until = @lockedUntil
WHERE {db.Dialect.InList("id", "ids")} AND status = 0 AND (locked_until IS NULL OR locked_until < @now)",
            new { token, lockedUntil = now + _options.LockTimeout, ids = ids.ToArray(), now },
            cancellationToken);

        return await db.QueryAsync<OutboxRow>(
            $@"SELECT id AS Id, message_id AS MessageId, exchange_name AS ExchangeName, routing_key AS RoutingKey,
connection_name AS ConnectionName, body AS Body, headers AS Headers, correlation_id AS CorrelationId,
message_type AS MessageType, retry_count AS RetryCount, lock_token AS LockToken
FROM {_options.OutboxTable} WHERE lock_token = @token ORDER BY creation_time",
            new { token },
            cancellationToken);
    }

    private async Task SendGroupAsync(
        IServiceProvider serviceProvider,
        RabbitMessagingDb db,
        string connectionName,
        List<OutboxRow> rows,
        CancellationToken cancellationToken)
    {
        var connection = connectionName.Length == 0
            ? serviceProvider.GetRequiredService<IRabbitConnection>()
            : new AbpConnectionPoolRabbitConnection(serviceProvider.GetRequiredService<IConnectionPool>(), connectionName);

        IChannel? channel = null;
        try
        {
            foreach (var row in rows)
            {
                try
                {
                    if (channel is not { IsOpen: true })
                    {
                        channel?.Dispose();
                        channel = await connection.CreateChannelAsync(
                            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                            cancellationToken);
                    }

                    var properties = new BasicProperties
                    {
                        DeliveryMode = DeliveryModes.Persistent,
                        MessageId = row.MessageId,
                        CorrelationId = row.CorrelationId,
                        Type = row.MessageType,
                        Headers = RabbitOutboxHeaders.Deserialize(row.Headers)
                    };

                    // With publisher confirms this completes only after the broker has accepted the message.
                    await channel.BasicPublishAsync(row.ExchangeName, row.RoutingKey, false, properties, row.Body, cancellationToken);
                    await db.ExecuteAsync(
                        $@"UPDATE {_options.OutboxTable} SET status = 1, sent_time = @now, lock_token = NULL, locked_until = NULL
WHERE id = @id AND lock_token = @token",
                        new { now = DateTime.UtcNow, id = row.Id, token = row.LockToken },
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    await MarkFailedAsync(db, row, ex, cancellationToken);
                }
            }
        }
        finally
        {
            channel?.Dispose();
        }
    }

    private async Task MarkFailedAsync(RabbitMessagingDb db, OutboxRow row, Exception exception, CancellationToken cancellationToken)
    {
        var retryCount = row.RetryCount + 1;
        var status = retryCount >= _options.MaxRetryCount ? RabbitOutboxStatus.Failed : RabbitOutboxStatus.Pending;
        var delayTicks = Math.Min(
            _options.RetryBaseDelay.Ticks * Math.Pow(2, Math.Min(retryCount - 1, 30)),
            _options.RetryMaxDelay.Ticks);
        var error = exception.Message.Length > 1000 ? exception.Message[..1000] : exception.Message;

        _logger.Log(
            status == RabbitOutboxStatus.Failed ? LogLevel.Error : LogLevel.Warning,
            exception,
            "Outbox send failed. MessageId={MessageId} Retry={Retry} Status={Status}",
            row.MessageId,
            retryCount,
            status);

        await db.ExecuteAsync(
            $@"UPDATE {_options.OutboxTable} SET status = @status, retry_count = @retryCount, next_retry_time = @next,
last_error = @error, lock_token = NULL, locked_until = NULL WHERE id = @id AND lock_token = @token",
            new
            {
                status,
                retryCount,
                next = DateTime.UtcNow + TimeSpan.FromTicks((long)delayTicks),
                error,
                id = row.Id,
                token = row.LockToken
            },
            cancellationToken);
    }

    private sealed class OutboxRow
    {
        public string Id { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;
        public string ExchangeName { get; set; } = string.Empty;
        public string RoutingKey { get; set; } = string.Empty;
        public string? ConnectionName { get; set; }
        public byte[] Body { get; set; } = [];
        public string? Headers { get; set; }
        public string? CorrelationId { get; set; }
        public string? MessageType { get; set; }
        public int RetryCount { get; set; }
        public string? LockToken { get; set; }
    }
}
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.Extensions.RabbitMQ.EntityFrameworkCore;

/// <summary>
/// Runs the handler inside a new transactional UoW and records (MessageId, ConsumerName) in the same
/// transaction, so handler writes and the inbox row commit or roll back together.
/// </summary>
public class RabbitInboxStore : IRabbitInboxStore
{
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly RabbitMessagingDbContextResolver _dbContextResolver;
    private readonly RabbitMessagingTableInitializer _tableInitializer;
    private readonly RabbitOutboxOptions _options;
    private readonly ILogger _logger;

    public RabbitInboxStore(
        IUnitOfWorkManager unitOfWorkManager,
        RabbitMessagingDbContextResolver dbContextResolver,
        RabbitMessagingTableInitializer tableInitializer,
        IOptions<RabbitOutboxOptions> options,
        ILogger<RabbitInboxStore>? logger = null)
    {
        _unitOfWorkManager = unitOfWorkManager;
        _dbContextResolver = dbContextResolver;
        _tableInitializer = tableInitializer;
        _options = options.Value;
        _logger = logger ?? NullLogger<RabbitInboxStore>.Instance;
    }

    public async Task<RabbitMessageResult> ProcessOnceAsync(
        string messageId,
        string consumerName,
        Func<Task<RabbitMessageResult>> handler,
        CancellationToken cancellationToken = default)
    {
        using var unitOfWork = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);

        // Pre-check when a DbContext is known up front (skips the handler for duplicates).
        var dbContext = await _dbContextResolver.GetConfiguredAsync();
        if (dbContext != null && await IsProcessedAsync(dbContext, messageId, consumerName, cancellationToken))
        {
            await unitOfWork.CompleteAsync(cancellationToken);
            return RabbitMessageResult.Success;
        }

        var result = await handler();
        if (result != RabbitMessageResult.Success)
        {
            return result; // dispose without Complete => rollback
        }

        dbContext ??= _dbContextResolver.GetActive()
                      ?? throw new InvalidOperationException(
                          "No EF Core DbContext for the inbox; set RabbitOutboxOptions.DbContextType");

        if (await IsProcessedAsync(dbContext, messageId, consumerName, cancellationToken))
        {
            return RabbitMessageResult.Success; // duplicate detected late => roll back handler writes
        }

        var db = await RabbitMessagingDb.FromAsync(dbContext, cancellationToken);
        await db.ExecuteAsync(
            $"INSERT INTO {_options.InboxTable} (message_id, consumer_name, processed_time) VALUES (@messageId, @consumerName, @now)",
            new { messageId, consumerName, now = DateTime.UtcNow },
            cancellationToken);

        // A concurrent duplicate fails on the primary key; the exception path retries and is then skipped.
        await unitOfWork.CompleteAsync(cancellationToken);
        return result;
    }

    private async Task<bool> IsProcessedAsync(IEfCoreDbContext dbContext, string messageId, string consumerName, CancellationToken cancellationToken)
    {
        await _tableInitializer.EnsureTablesAsync(dbContext, cancellationToken);
        var db = await RabbitMessagingDb.FromAsync(dbContext, cancellationToken);
        var count = await db.ScalarAsync<long>(
            $"SELECT COUNT(1) FROM {_options.InboxTable} WHERE message_id = @messageId AND consumer_name = @consumerName",
            new { messageId, consumerName },
            cancellationToken);
        if (count > 0)
        {
            _logger.LogInformation("Duplicate message {MessageId} skipped by consumer {Consumer}", messageId, consumerName);
        }

        return count > 0;
    }
}
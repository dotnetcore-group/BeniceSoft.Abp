using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;
using Volo.Abp.Uow.EntityFrameworkCore;

namespace BeniceSoft.Abp.Extensions.RabbitMQ.EntityFrameworkCore;

/// <summary>Raw-SQL access bound to a DbContext's connection and current transaction.</summary>
public sealed class RabbitMessagingDb
{
    private RabbitMessagingDb(DbConnection connection, DbTransaction? transaction, RabbitMessagingDialect dialect)
    {
        Connection = connection;
        Transaction = transaction;
        Dialect = dialect;
    }

    public DbConnection Connection { get; }

    public DbTransaction? Transaction { get; }

    public RabbitMessagingDialect Dialect { get; }

    public static async Task<RabbitMessagingDb> FromAsync(IEfCoreDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var dialect = RabbitMessagingDialect.For(dbContext.Database.ProviderName);
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        return new RabbitMessagingDb(connection, dbContext.Database.CurrentTransaction?.GetDbTransaction(), dialect);
    }

    public Task<int> ExecuteAsync(string sql, object? param = null, CancellationToken cancellationToken = default)
        => Connection.ExecuteAsync(new CommandDefinition(sql, param, Transaction, cancellationToken: cancellationToken));

    public Task<T?> ScalarAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
        => Connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, param, Transaction, cancellationToken: cancellationToken));

    public async Task<List<T>> QueryAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
        => (await Connection.QueryAsync<T>(new CommandDefinition(sql, param, Transaction, cancellationToken: cancellationToken))).ToList();
}

/// <summary>
/// Picks the DbContext for outbox/inbox SQL: the one already active in the current UoW (same transaction
/// as the business writes), else <see cref="RabbitOutboxOptions.DbContextType"/>.
/// </summary>
public class RabbitMessagingDbContextResolver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly RabbitOutboxOptions _options;

    public RabbitMessagingDbContextResolver(
        IServiceProvider serviceProvider,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<RabbitOutboxOptions> options)
    {
        _serviceProvider = serviceProvider;
        _unitOfWorkManager = unitOfWorkManager;
        _options = options.Value;
    }

    public IEfCoreDbContext? GetActive()
        => (_unitOfWorkManager.Current as UnitOfWork)?
            .GetAllActiveDatabaseApis()
            .OfType<EfCoreDatabaseApi>()
            .Select(x => x.DbContext)
            .FirstOrDefault();

    public async Task<IEfCoreDbContext?> GetConfiguredAsync()
    {
        if (_options.DbContextType == null)
        {
            return null;
        }

        var providerType = typeof(IDbContextProvider<>).MakeGenericType(_options.DbContextType);
        var provider = _serviceProvider.GetRequiredService(providerType);
        var task = (Task)providerType.GetMethod(nameof(IDbContextProvider<IEfCoreDbContext>.GetDbContextAsync))!.Invoke(provider, null)!;
        await task;
        return (IEfCoreDbContext)task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    public async Task<IEfCoreDbContext?> ResolveAsync(bool preferActive)
        => (preferActive ? GetActive() : null) ?? await GetConfiguredAsync() ?? GetActive();
}

/// <summary>Creates the outbox/inbox tables if missing (idempotent, once per connection string), like the DTM barrier table.</summary>
public class RabbitMessagingTableInitializer
{
    private readonly ConcurrentDictionary<string, bool> _created = new();
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly RabbitOutboxOptions _options;
    private readonly ILogger<RabbitMessagingTableInitializer> _logger;

    public RabbitMessagingTableInitializer(
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<RabbitOutboxOptions> options,
        ILogger<RabbitMessagingTableInitializer> logger)
    {
        _unitOfWorkManager = unitOfWorkManager;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnsureTablesAsync(IEfCoreDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var connectionString = dbContext.Database.GetConnectionString() ?? string.Empty;
        if (_created.ContainsKey(connectionString))
        {
            return;
        }

        var dialect = RabbitMessagingDialect.For(dbContext.Database.ProviderName);
        var sql = dialect.CreateTablesSql(_options);
        var factory = dialect.DdlOnBusinessConnection ? null : DbProviderFactories.GetFactory(dbContext.Database.GetDbConnection());

        if (factory == null)
        {
            var db = await RabbitMessagingDb.FromAsync(dbContext, cancellationToken);
            await db.ExecuteAsync(sql, cancellationToken: cancellationToken);
            if (db.Transaction != null && _unitOfWorkManager.Current != null)
            {
                // DDL may be rolled back with the business transaction (SQLite); only cache after commit.
                _unitOfWorkManager.Current.OnCompleted(() =>
                {
                    _created[connectionString] = true;
                    return Task.CompletedTask;
                });
                return;
            }
        }
        else
        {
            try
            {
                await using var connection = factory.CreateConnection()!;
                connection.ConnectionString = connectionString;
                await connection.OpenAsync(cancellationToken);
                await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
            }
            catch (Exception ex)
            {
                // e.g. a concurrent instance created it first; the following statements will surface real problems.
                _logger.LogWarning(ex, "Creating RabbitMQ outbox/inbox tables failed");
                return;
            }
        }

        _logger.LogInformation("RabbitMQ outbox/inbox tables ensured: {Outbox}, {Inbox}", _options.OutboxTable, _options.InboxTable);
        _created[connectionString] = true;
    }
}
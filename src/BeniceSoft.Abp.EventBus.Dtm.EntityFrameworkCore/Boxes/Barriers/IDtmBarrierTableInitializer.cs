using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.EventBus.Dtm.EntityFrameworkCore;

public interface IDtmBarrierTableInitializer
{
    Task TryCreateTableAsync(IEfCoreDbContext dbContext);
}

public class DtmBarrierTableInitializer : IDtmBarrierTableInitializer, ISingletonDependency
{
    protected IUnitOfWorkManager UnitOfWorkManager { get; }
    private ILogger<DtmBarrierTableInitializer> Logger { get; }
    private ConcurrentDictionary<string, bool> CreatedConnectionStrings { get; } = new();

    protected DtmEventBoxesOptions Options { get; }

    public DtmBarrierTableInitializer(
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<DtmBarrierTableInitializer> logger,
        IOptions<DtmEventBoxesOptions> options)
    {
        UnitOfWorkManager = unitOfWorkManager;
        Logger = logger;
        Options = options.Value;
    }

    public virtual async Task TryCreateTableAsync(IEfCoreDbContext dbContext)
    {
        var connectionString = dbContext.Database.GetConnectionString() ?? string.Empty;

        if (CreatedConnectionStrings.ContainsKey(connectionString))
        {
            return;
        }

        var providerName = dbContext.Database.ProviderName;
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new NotSupportedException("Current database provider name is empty, cannot initialize DTM barrier table.");
        }

        var special = BarrierSqlTemplates.DbProviderSpecialMapping.GetOrDefault(providerName);

        Logger.LogInformation("DtmBarrierTableInitializer found database provider: {databaseName}",
            providerName);

        if (special is null)
        {
            throw new NotSupportedException(
                $"Database provider {dbContext.Database.ProviderName} is not supported by the DTM event boxes!");
        }

        if (string.IsNullOrWhiteSpace(Options.BarrierTableName))
        {
            Logger.LogWarning("DtmEventBoxesOptions.BarrierTableName 未配置，已使用数据库默认屏障表名。");
        }

        var sql = special.GetCreateBarrierTableSql(Options);
        var currentTransaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();

        await dbContext.Database.GetDbConnection().ExecuteAsync(sql, null, currentTransaction);

        var currentUow = UnitOfWorkManager.Current;
        if (currentUow is null)
        {
            CreatedConnectionStrings[connectionString] = true;
            return;
        }

        currentUow.OnCompleted(() =>
        {
            CreatedConnectionStrings[connectionString] = true;
            return Task.CompletedTask;
        });
    }
}
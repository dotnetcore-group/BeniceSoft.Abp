using Dapper;
using DtmCommon;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace BeniceSoft.Abp.EventBus.Dtm.EntityFrameworkCore;

public class AbpEfCoreDtmInboxBarrierManager : IDtmInboxBarrierManager<IEfCoreDbContext>, ITransientDependency
{
    protected DtmEventBoxesOptions Options { get; }
    private ILogger<AbpEfCoreDtmInboxBarrierManager> Logger { get; }
    protected IDtmBarrierTableInitializer BarrierTableInitializer { get; }

    public AbpEfCoreDtmInboxBarrierManager(
        IOptions<DtmEventBoxesOptions> options,
        ILogger<AbpEfCoreDtmInboxBarrierManager> logger,
        IDtmBarrierTableInitializer barrierTableInitializer)
    {
        Options = options.Value;
        Logger = logger;
        BarrierTableInitializer = barrierTableInitializer;
    }

    public virtual async Task EnsureInsertBarrierAsync(IEfCoreDbContext dbContext, string gid)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new AbpException("DTM barrier is for ABP transactional events.");
        }

        var affected = await InsertBarrierAsync(dbContext, gid, InboxBarrierProperties.Reason);

        Logger?.LogDebug("currentAffected: {currentAffected}", affected);

        if (affected == 0)
        {
            throw new DtmDuplicatedException();
        }
    }

    public virtual async Task<bool> ExistBarrierAsync(IEfCoreDbContext dbContext, string gid)
    {
        await BarrierTableInitializer.TryCreateTableAsync(dbContext);

        var special = GetSpecial(dbContext);

        var reason = await dbContext.Database.GetDbConnection().QueryFirstOrDefaultAsync<string>(
            special.GetQueryPreparedSql(Options.BarrierTableName),
            new
            {
                gid,
                branch_id = InboxBarrierProperties.BranchId,
                op = InboxBarrierProperties.Op,
                barrier_id = InboxBarrierProperties.BarrierId
            });

        return !reason.IsNullOrEmpty();
    }

    protected virtual async Task<int> InsertBarrierAsync(IEfCoreDbContext dbContext, string gid, string reason)
    {
        await BarrierTableInitializer.TryCreateTableAsync(dbContext);

        var special = GetSpecial(dbContext);

        var sql = special.GetInsertIgnoreTemplate(Options.BarrierTableName);

        sql = special.GetPlaceHoldSQL(sql);

        var affected = await dbContext.Database.GetDbConnection().ExecuteAsync(
            sql,
            new
            {
                trans_type = InboxBarrierProperties.TransType,
                gid = gid,
                branch_id = InboxBarrierProperties.BranchId,
                op = InboxBarrierProperties.Op,
                barrier_id = InboxBarrierProperties.BarrierId,
                reason = reason
            },
            dbContext.Database.CurrentTransaction?.GetDbTransaction());

        return affected;
    }

    protected virtual IDtmBarrierDbSpecial GetSpecial(IEfCoreDbContext dbContext)
    {
        var providerName = dbContext.Database.ProviderName ?? "";
        if (string.IsNullOrEmpty(providerName))
        {
            throw new NotSupportedException(
                $"Database provider name is empty!");
        }

        var special = BarrierSqlTemplates.DbProviderSpecialMapping.GetOrDefault(providerName) ?? throw new NotSupportedException(
                $"Database provider {dbContext.Database.ProviderName} is not supported by the event boxes!");
        return special;
    }
}

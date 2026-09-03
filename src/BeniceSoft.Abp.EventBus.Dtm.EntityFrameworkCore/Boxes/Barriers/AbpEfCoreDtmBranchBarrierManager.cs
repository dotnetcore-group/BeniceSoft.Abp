using System.Collections.Concurrent;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace BeniceSoft.Abp.EventBus.Dtm.EntityFrameworkCore;

public class AbpEfCoreDtmBranchBarrierManager : IAbpEfCoreDtmBranchBarrierManager, ITransientDependency
{
    protected IServiceProvider ServiceProvider { get; }
    protected IConnectionStringResolver ConnectionStringResolver { get; }
    protected IConnectionStringHasher ConnectionStringHasher { get; }
    protected IDtmBarrierTableInitializer BarrierTableInitializer { get; }
    protected DtmEventBoxesOptions Options { get; }
    protected ILogger<AbpEfCoreDtmBranchBarrierManager> Logger { get; }
    protected static ConcurrentDictionary<string, DbContextProviderInfo> CachedDbContextProviderInfo { get; } = new();

    public AbpEfCoreDtmBranchBarrierManager(
        IServiceProvider serviceProvider,
        IConnectionStringResolver connectionStringResolver,
        IConnectionStringHasher connectionStringHasher,
        IDtmBarrierTableInitializer barrierTableInitializer,
        IOptions<DtmEventBoxesOptions> options,
        ILogger<AbpEfCoreDtmBranchBarrierManager> logger)
    {
        ServiceProvider = serviceProvider;
        ConnectionStringResolver = connectionStringResolver;
        ConnectionStringHasher = connectionStringHasher;
        BarrierTableInitializer = barrierTableInitializer;
        Options = options.Value;
        Logger = logger;
    }

    public virtual async Task<DtmBranchBarrierInsertResult> TryInsertBarrierAsync(
        DtmBranchBarrierInfo barrierInfo,
        string? dbContextTypeName,
        string? hashedConnectionString,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dbContextTypeName))
        {
            return DtmBranchBarrierInsertResult.NotHandled;
        }

        var providerInfo = GetDbContextProviderInfoOrNull(dbContextTypeName);
        if (providerInfo is null)
        {
            return DtmBranchBarrierInsertResult.NotHandled;
        }

        if (!string.IsNullOrWhiteSpace(hashedConnectionString))
        {
            var connectionString = await ConnectionStringResolver.ResolveAsync(providerInfo.DbContextType);
            var hash = await ConnectionStringHasher.HashAsync(connectionString);
            if (!string.Equals(hash, hashedConnectionString, StringComparison.Ordinal))
            {
                throw new AbpException($"DTM branch barrier with a wrong HashedConnectionString, gid: {barrierInfo.Gid}");
            }
        }

        var dbContextProvider = ServiceProvider.GetRequiredService(providerInfo.DbContextProviderType);
        dynamic task = providerInfo.GetDbContextAsyncMethodInfo.Invoke(dbContextProvider, null)!;
        IEfCoreDbContext dbContext = await task;

        await BarrierTableInitializer.TryCreateTableAsync(dbContext);

        var affected = await InsertBarrierAsync(dbContext, barrierInfo);
        if (affected == 0)
        {
            Logger.LogInformation(
                "DTM BranchBarrier duplicated, Gid={Gid}, TransType={TransType}, BranchId={BranchId}, Op={Op}, BarrierId={BarrierId}",
                barrierInfo.Gid, barrierInfo.TransType, barrierInfo.BranchId, barrierInfo.Op, barrierInfo.BarrierId);
            return DtmBranchBarrierInsertResult.Duplicated;
        }

        return DtmBranchBarrierInsertResult.Inserted;
    }

    protected virtual async Task<int> InsertBarrierAsync(IEfCoreDbContext dbContext, DtmBranchBarrierInfo barrierInfo)
    {
        var providerName = dbContext.Database.ProviderName;
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new NotSupportedException("Current database provider name is empty, cannot execute DTM branch barrier.");
        }

        var special = BarrierSqlTemplates.DbProviderSpecialMapping.GetOrDefault(providerName);
        if (special is null)
        {
            throw new NotSupportedException(
                $"Database provider {providerName} is not supported by the event boxes!");
        }

        var currentTransaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        if (currentTransaction is null)
        {
            throw new AbpException("DTM branch barrier must run inside an active transaction.");
        }

        var sql = special.GetInsertIgnoreTemplate(Options.BarrierTableName);
        sql = special.GetPlaceHoldSQL(sql);

        return await dbContext.Database.GetDbConnection().ExecuteAsync(
            sql,
            new
            {
                trans_type = barrierInfo.TransType,
                gid = barrierInfo.Gid,
                branch_id = barrierInfo.BranchId,
                op = barrierInfo.Op,
                barrier_id = barrierInfo.BarrierId,
                reason = barrierInfo.Reason
            },
            currentTransaction);

    }

    protected virtual DbContextProviderInfo? GetDbContextProviderInfoOrNull(string dbContextTypeName)
    {
        if (CachedDbContextProviderInfo.TryGetValue(dbContextTypeName, out var cachedInfo))
        {
            return cachedInfo;
        }

        var dbContextType = Type.GetType(dbContextTypeName);
        if (dbContextType is null || !dbContextType.IsAssignableTo(typeof(IEfCoreDbContext)))
        {
            return null;
        }

        var providerType = typeof(IDbContextProvider<>).MakeGenericType(dbContextType);
        var getDbContextAsyncMethodInfo = providerType.GetMethod("GetDbContextAsync");
        if (getDbContextAsyncMethodInfo is null)
        {
            return null;
        }

        var providerInfo = new DbContextProviderInfo(dbContextType, providerType, getDbContextAsyncMethodInfo);
        CachedDbContextProviderInfo[dbContextTypeName] = providerInfo;

        return providerInfo;
    }
}

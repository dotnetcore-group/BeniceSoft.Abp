using JetBrains.Annotations;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.EventBus.Dtm;

public interface IDtmMsgBarrierManager
{
    /// <summary>
    /// Invokes InsertBarrierAsync method if the databaseApi can be identified.
    /// </summary>
    Task<bool> TryInvokeEnsureInsertBarrierAsync(IDatabaseApi databaseApi, [NotNull] string gid,
        CancellationToken cancellationToken = default);
}

public interface IDtmMsgBarrierManager<in TDbContextInterface> : IDtmMsgBarrierManager where TDbContextInterface : class
{
    Task EnsureInsertBarrierAsync(TDbContextInterface dbContext, [NotNull] string gid,
        CancellationToken cancellationToken = default);

    Task<bool> TryInsertBarrierAsRollbackAsync(TDbContextInterface dbContext, [NotNull] string gid,
        CancellationToken cancellationToken = default);
}

public abstract class DtmMsgBarrierManagerBase<TDbContextInterface> : IDtmMsgBarrierManager<TDbContextInterface>, IDtmMsgBarrierManager
    where TDbContextInterface : class
{
    public abstract Task<bool> TryInvokeEnsureInsertBarrierAsync(IDatabaseApi databaseApi, string gid,
        CancellationToken cancellationToken = default);

    protected virtual bool IsValidDatabaseApi<TDatabaseApi>(IDatabaseApi databaseApi) where TDatabaseApi : IDatabaseApi
    {
        return databaseApi.GetType().IsAssignableTo(typeof(TDatabaseApi));
    }

    public abstract Task EnsureInsertBarrierAsync(TDbContextInterface dbContext, string gid,
        CancellationToken cancellationToken = default);

    public abstract Task<bool> TryInsertBarrierAsRollbackAsync(TDbContextInterface dbContext, string gid,
        CancellationToken cancellationToken = default);
}
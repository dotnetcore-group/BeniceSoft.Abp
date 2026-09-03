using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.EventBus.Dtm.EntityFrameworkCore;

/// <summary>
/// DTM Outbox 实现，支持动态获取 DbContext
/// </summary>
public class DtmDbContextEventOutbox : IEventOutbox
{
    protected IUnitOfWorkManager UnitOfWorkManager { get; }
    protected IDbContextProvider<IEfCoreDbContext> DbContextProvider { get; }
    protected IDtmMsgManager DtmMessageManager { get; }

    public DtmDbContextEventOutbox(
        IUnitOfWorkManager unitOfWorkManager,
        IDbContextProvider<IEfCoreDbContext> dbContextProvider,
        IDtmMsgManager dtmMessageManager)
    {
        UnitOfWorkManager = unitOfWorkManager;
        DbContextProvider = dbContextProvider;
        DtmMessageManager = dtmMessageManager;
    }

    public virtual async Task EnqueueAsync(OutgoingEventInfo outgoingEvent)
    {
        var dbContext = await DbContextProvider.GetDbContextAsync();

        await DtmMessageManager.AddEventAsync(
            ((DtmUnitOfWork)UnitOfWorkManager.Current!).GetDtmOutboxEventBag(),
            dbContext,
            dbContext.Database.GetConnectionString() ?? throw new InvalidOperationException(),
            dbContext.Database.CurrentTransaction?.GetDbTransaction(),
            outgoingEvent);
    }

    public virtual Task<List<OutgoingEventInfo>> GetWaitingEventsAsync(
        int maxCount,
        Expression<Func<IOutgoingEventInfo, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public virtual Task DeleteAsync(Guid id)
    {
        throw new NotSupportedException();
    }

    public virtual Task DeleteManyAsync(IEnumerable<Guid> ids)
    {
        throw new NotSupportedException();
    }
}

public class DtmDbContextEventOutbox<TDbContext> : IDtmDbContextEventOutbox<TDbContext>
    where TDbContext : IEfCoreDbContext
{
    protected IUnitOfWorkManager UnitOfWorkManager { get; }
    protected IDbContextProvider<TDbContext> DbContextProvider { get; }
    protected IDtmMsgManager DtmMessageManager { get; }

    public DtmDbContextEventOutbox(
        IUnitOfWorkManager unitOfWorkManager,
        IDbContextProvider<TDbContext> dbContextProvider,
        IDtmMsgManager dtmMessageManager)
    {
        UnitOfWorkManager = unitOfWorkManager;
        DbContextProvider = dbContextProvider;
        DtmMessageManager = dtmMessageManager;
    }

    public virtual async Task EnqueueAsync(OutgoingEventInfo outgoingEvent)
    {
        var dbContext = await DbContextProvider.GetDbContextAsync();

        await DtmMessageManager.AddEventAsync(
            ((DtmUnitOfWork)UnitOfWorkManager.Current!).GetDtmOutboxEventBag(),
            dbContext,
            dbContext.Database.GetConnectionString() ?? throw new InvalidOperationException(),
            dbContext.Database.CurrentTransaction?.GetDbTransaction(),
            outgoingEvent);
    }

    public virtual Task<List<OutgoingEventInfo>> GetWaitingEventsAsync(
        int maxCount,
        Expression<Func<IOutgoingEventInfo, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public virtual Task DeleteAsync(Guid id)
    {
        throw new NotSupportedException();
    }

    public virtual Task DeleteManyAsync(IEnumerable<Guid> ids)
    {
        throw new NotSupportedException();
    }
}

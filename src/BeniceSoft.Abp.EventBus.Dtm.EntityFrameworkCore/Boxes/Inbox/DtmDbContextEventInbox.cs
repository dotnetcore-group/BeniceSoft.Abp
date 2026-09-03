using System.Linq.Expressions;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.EventBus.Dtm.EntityFrameworkCore;

/// <summary>
/// DTM Inbox 实现，动态获取 DbContext
/// </summary>
public class DtmDbContextEventInbox : IEventInbox
{
    protected IUnitOfWorkManager UnitOfWorkManager { get; }
    protected IDistributedEventBus DistributedEventBus { get; }
    protected IDbContextProvider<IEfCoreDbContext> DbContextProvider { get; }
    protected IDtmInboxBarrierManager<IEfCoreDbContext> DtmInboxBarrierManager { get; }

    public DtmDbContextEventInbox(
        IUnitOfWorkManager unitOfWorkManager,
        IDistributedEventBus distributedEventBus,
        IDbContextProvider<IEfCoreDbContext> dbContextProvider,
        IDtmInboxBarrierManager<IEfCoreDbContext> dtmInboxBarrierManager)
    {
        UnitOfWorkManager = unitOfWorkManager;
        DistributedEventBus = distributedEventBus;
        DbContextProvider = dbContextProvider;
        DtmInboxBarrierManager = dtmInboxBarrierManager;
    }

    public virtual async Task EnqueueAsync(IncomingEventInfo incomingEvent)
    {
        using var uow = UnitOfWorkManager.Begin(isTransactional: true, requiresNew: true);

        var dbContext = await DbContextProvider.GetDbContextAsync();

        await DtmInboxBarrierManager.EnsureInsertBarrierAsync(dbContext, incomingEvent.MessageId);

        await DistributedEventBus
            .AsSupportsEventBoxes()
            .ProcessFromInboxAsync(incomingEvent, new InboxConfig("DTM_InBox"));

        await uow.CompleteAsync();
    }

    public virtual Task<List<IncomingEventInfo>> GetWaitingEventsAsync(
        int maxCount,
        Expression<Func<IIncomingEventInfo, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public virtual Task MarkAsProcessedAsync(Guid id)
    {
        throw new NotSupportedException();
    }

    public virtual Task RetryLaterAsync(Guid id, int retryCount, DateTime? nextRetryTime)
    {
        throw new NotSupportedException();
    }

    public virtual Task MarkAsDiscardAsync(Guid id)
    {
        throw new NotSupportedException();
    }

    [UnitOfWork(false)]
    public virtual async Task<bool> ExistsByMessageIdAsync(string messageId)
    {
        var dbContext = await DbContextProvider.GetDbContextAsync();
        return await DtmInboxBarrierManager.ExistBarrierAsync(dbContext, messageId);
    }

    public virtual Task DeleteOldEventsAsync()
    {
        throw new NotSupportedException();
    }
}

public class DtmDbContextEventInbox<TDbContext> : IDtmDbContextEventInbox<TDbContext>
    where TDbContext : IEfCoreDbContext
{
    protected IUnitOfWorkManager UnitOfWorkManager { get; }
    protected IDistributedEventBus DistributedEventBus { get; }
    protected IDbContextProvider<TDbContext> DbContextProvider { get; }
    protected IDtmInboxBarrierManager<IEfCoreDbContext> DtmInboxBarrierManager { get; }

    public DtmDbContextEventInbox(
        IUnitOfWorkManager unitOfWorkManager,
        IDistributedEventBus distributedEventBus,
        IDbContextProvider<TDbContext> dbContextProvider,
        IDtmInboxBarrierManager<IEfCoreDbContext> dtmInboxBarrierManager)
    {
        UnitOfWorkManager = unitOfWorkManager;
        DistributedEventBus = distributedEventBus;
        DbContextProvider = dbContextProvider;
        DtmInboxBarrierManager = dtmInboxBarrierManager;
    }

    public virtual async Task EnqueueAsync(IncomingEventInfo incomingEvent)
    {
        using var uow = UnitOfWorkManager.Begin(isTransactional: true, requiresNew: true);

        var dbContext = await DbContextProvider.GetDbContextAsync();

        await DtmInboxBarrierManager.EnsureInsertBarrierAsync(dbContext, incomingEvent.MessageId);

        await DistributedEventBus
            .AsSupportsEventBoxes()
            .ProcessFromInboxAsync(incomingEvent, new InboxConfig("DTM_Inbox"));

        await uow.CompleteAsync();
    }

    public virtual Task<List<IncomingEventInfo>> GetWaitingEventsAsync(
        int maxCount,
        Expression<Func<IIncomingEventInfo, bool>>? filter = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public virtual Task MarkAsProcessedAsync(Guid id)
    {
        throw new NotSupportedException();
    }

    public virtual Task RetryLaterAsync(Guid id, int retryCount, DateTime? nextRetryTime)
    {
        throw new NotSupportedException();
    }

    public virtual Task MarkAsDiscardAsync(Guid id)
    {
        throw new NotSupportedException();
    }

    [UnitOfWork(false)]
    public virtual async Task<bool> ExistsByMessageIdAsync(string messageId)
    {
        var dbContext = await DbContextProvider.GetDbContextAsync();

        return await DtmInboxBarrierManager.ExistBarrierAsync(dbContext, messageId);
    }

    public virtual Task DeleteOldEventsAsync()
    {
        throw new NotSupportedException();
    }
}

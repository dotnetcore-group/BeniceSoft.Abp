using Microsoft.Extensions.Options;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.EventBus.Dtm;

public class DtmUnitOfWork : UnitOfWork
{
    protected IDtmMsgManager DtmMessageManager { get; }

    protected DtmOutboxEventBag EventBag { get; } = new();

    public DtmUnitOfWork(
        IServiceProvider serviceProvider,
        IDtmMsgManager dtmMessageManager,
        IUnitOfWorkEventPublisher unitOfWorkEventPublisher,
        IOptions<AbpUnitOfWorkDefaultOptions> options)
        : base(serviceProvider, unitOfWorkEventPublisher, options)
    {
        DtmMessageManager = dtmMessageManager;
    }

    protected override async Task CommitTransactionsAsync(CancellationToken cancellationToken)
    {
        if (!EventBag.HasAnyEvent())
        {
            await base.CommitTransactionsAsync(cancellationToken);
            return;
        }

        OnCompleted(async () => await DtmMessageManager.SubmitAsync(EventBag, cancellationToken));

        await DtmMessageManager.PrepareAndInsertBarriersAsync(EventBag, cancellationToken);

        await base.CommitTransactionsAsync(cancellationToken);
    }

    public virtual DtmOutboxEventBag GetDtmOutboxEventBag()
    {
        return EventBag;
    }
}

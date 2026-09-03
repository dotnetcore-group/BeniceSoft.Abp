using JetBrains.Annotations;
using Volo.Abp.EventBus.Distributed;

namespace BeniceSoft.Abp.EventBus.Dtm;

public interface IDtmMsgManager
{
    Task AddEventAsync(
        DtmOutboxEventBag eventBag,
        object dbContext,
        [NotNull] string connectionString,
        [CanBeNull] object? transObj,
        OutgoingEventInfo eventInfo);

    Task PrepareAndInsertBarriersAsync(DtmOutboxEventBag eventBag, CancellationToken cancellationToken = default);

    Task SubmitAsync(DtmOutboxEventBag eventBag, CancellationToken cancellationToken = default);
}

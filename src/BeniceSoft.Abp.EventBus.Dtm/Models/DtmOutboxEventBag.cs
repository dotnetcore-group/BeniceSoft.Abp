using JetBrains.Annotations;

namespace BeniceSoft.Abp.EventBus.Dtm;

public class DtmOutboxEventBag
{
    /// <summary>
    /// DTM message for non-transactional distributed events.
    /// </summary>
    [CanBeNull]
    public IDtmMsgInfoModel? DefaultMessage { get; set; }

    /// <summary>
    /// DTM message for each transaction. Mapping from transaction objects to message models.
    /// </summary>
    public Dictionary<object, IDtmMsgInfoModel> TransMessages { get; } = [];

    public bool HasAnyEvent()
    {
        return DefaultMessage is not null || TransMessages.Count != 0;
    }
}

using BeniceSoft.Abp.EventBus.Dtm;

namespace BeniceSoft.Abp.EventBus.Dtm.EntityFrameworkCore;

public interface IAbpEfCoreDtmBranchBarrierManager : ITccBranchBarrierManager, ISagaBranchBarrierManager
{
}


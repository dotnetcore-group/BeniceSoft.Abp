using Volo.Abp.EntityFrameworkCore;

namespace BeniceSoft.Abp.EventBus.Dtm.EntityFrameworkCore;

public interface IAbpEfCoreDtmMsgBarrierManager : IDtmMsgBarrierManager<IEfCoreDbContext>
{
}

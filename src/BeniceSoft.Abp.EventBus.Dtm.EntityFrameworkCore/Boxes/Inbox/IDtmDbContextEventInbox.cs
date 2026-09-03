using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EventBus.Distributed;

namespace BeniceSoft.Abp.EventBus.Dtm.EntityFrameworkCore;

public interface IDtmDbContextEventInbox<TDbContext> : IEventInbox where TDbContext : IEfCoreDbContext
{

}

using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EventBus.Distributed;

namespace BeniceSoft.Abp.EventBus.Dtm.EntityFrameworkCore;

public interface IDtmDbContextEventOutbox<TDbContext> : IEventOutbox where TDbContext : IEfCoreDbContext
{
}

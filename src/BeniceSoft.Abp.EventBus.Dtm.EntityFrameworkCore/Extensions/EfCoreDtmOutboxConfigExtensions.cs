using Volo.Abp.EventBus.Distributed;

namespace BeniceSoft.Abp.EventBus.Dtm.EntityFrameworkCore;

public static class EfCoreDtmOutboxConfigExtensions
{
    public static void UseDbContextWithDtmOutbox(this OutboxConfig outboxConfig, Type dbContextType)
    {
        outboxConfig.ImplementationType = typeof(IDtmDbContextEventOutbox<>).MakeGenericType(dbContextType);
    }
}

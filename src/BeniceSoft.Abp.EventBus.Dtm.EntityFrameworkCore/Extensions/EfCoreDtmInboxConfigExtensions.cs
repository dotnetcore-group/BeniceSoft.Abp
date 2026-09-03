using Volo.Abp.EventBus.Distributed;

namespace BeniceSoft.Abp.EventBus.Dtm.EntityFrameworkCore;

public static class EfCoreDtmInboxConfigExtensions
{
    public static void UseDbContextWithDtmInbox(this InboxConfig inboxConfig, Type dbContextType)
    {
        inboxConfig.ImplementationType = typeof(IDtmDbContextEventInbox<>).MakeGenericType(dbContextType);
    }
}

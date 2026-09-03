using Volo.Abp.EventBus.Distributed;

namespace BeniceSoft.Abp.EventBus.Dtm;

/// <summary>
/// The DTM event inbox doesn't need a processor.
/// </summary>
public class DtmInboxProcessor : IInboxProcessor
{
    public virtual async Task StartAsync(InboxConfig inboxConfig, CancellationToken cancellationToken = new())
    {
        await Task.CompletedTask;
    }

    public virtual async Task StopAsync(CancellationToken cancellationToken = new())
    {
        await Task.CompletedTask;
    }
}

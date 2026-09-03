using Volo.Abp.EventBus.Distributed;

namespace BeniceSoft.Abp.EventBus.Dtm;

/// <summary>
/// The DTM event outbox doesn't need a sender.
/// </summary>
public class DtmOutboxSender : IOutboxSender
{
    public virtual async Task StartAsync(OutboxConfig outboxConfig, CancellationToken cancellationToken = new())
    {
        await Task.CompletedTask;
    }

    public virtual async Task StopAsync(CancellationToken cancellationToken = new())
    {
        await Task.CompletedTask;
    }
}

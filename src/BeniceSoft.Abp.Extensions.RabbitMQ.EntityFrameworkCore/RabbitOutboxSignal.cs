namespace BeniceSoft.Abp.Extensions.RabbitMQ.EntityFrameworkCore;

/// <summary>Wakes the dispatcher right after a UoW containing outbox rows commits.</summary>
public sealed class RabbitOutboxSignal
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    public void Notify()
    {
        try
        {
            _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        => _semaphore.WaitAsync(timeout, cancellationToken);
}
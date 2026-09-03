namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public interface ITccBranchCallbackHandler<in TRequest>
{
    Task TryAsync(TRequest request, CancellationToken cancellationToken = default);

    Task ConfirmAsync(TRequest request, CancellationToken cancellationToken = default);

    Task CancelAsync(TRequest request, CancellationToken cancellationToken = default);
}
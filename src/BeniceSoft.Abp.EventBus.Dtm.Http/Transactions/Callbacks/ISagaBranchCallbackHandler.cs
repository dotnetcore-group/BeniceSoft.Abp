namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public interface ISagaBranchCallbackHandler<in TRequest>
{
    Task ActionAsync(TRequest request, CancellationToken cancellationToken = default);

    Task CompensateAsync(TRequest request, CancellationToken cancellationToken = default);
}
namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public interface ISagaTransactionContext
{
    string Gid { get; }

    void AddBranch(string actionUrl, string compensateUrl, object body);

    void AddBranchByHandler(string serviceName, string handlerName, object body);

    void AddBranchByHandler<TRequest>(
        TRequest body,
        string? serviceName = null,
        string? handlerName = null)
        where TRequest : class, IBranchRequest;

    Task SubmitAsync(CancellationToken cancellationToken = default);
}
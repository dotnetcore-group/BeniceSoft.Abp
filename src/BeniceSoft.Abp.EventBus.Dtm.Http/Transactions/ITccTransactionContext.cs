namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public interface ITccTransactionContext
{
    string Gid { get; }

    Task CallBranchAsync<TRequest>(
        TRequest body,
        string? serviceName = null,
        string? handlerName = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
        where TRequest : class, IBranchRequest;
}

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public interface ITccGlobalTransactionManager
{
    Task ExecuteAsync(
        Func<ITccTransactionContext, Task> action,
        string? gid = null,
        Action<DtmGlobalTransactionOptions>? configure = null,
        CancellationToken cancellationToken = default);

    Task<TResult> ExecuteAsync<TResult>(
        Func<ITccTransactionContext, Task<TResult>> action,
        string? gid = null,
        Action<DtmGlobalTransactionOptions>? configure = null,
        CancellationToken cancellationToken = default);
}
namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public interface ISagaGlobalTransactionManager
{
    Task ExecuteAsync(
        Func<ISagaTransactionContext, Task> action,
        string? gid = null,
        Action<DtmGlobalTransactionOptions>? configure = null,
        CancellationToken cancellationToken = default);

    Task<TResult> ExecuteAsync<TResult>(
        Func<ISagaTransactionContext, Task<TResult>> action,
        string? gid = null,
        Action<DtmGlobalTransactionOptions>? configure = null,
        CancellationToken cancellationToken = default);
}
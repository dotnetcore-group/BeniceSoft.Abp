namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public interface IGlobalTransaction
{
    Task ExecuteTccAsync(
        Func<ITccTransactionContext, Task> action,
        string? gid = null,
        Action<DtmGlobalTransactionOptions>? configure = null,
        CancellationToken cancellationToken = default);

    Task<TResult> ExecuteTccAsync<TResult>(
        Func<ITccTransactionContext, Task<TResult>> action,
        string? gid = null,
        Action<DtmGlobalTransactionOptions>? configure = null,
        CancellationToken cancellationToken = default);

    Task ExecuteSagaAsync(
        Func<ISagaTransactionContext, Task> action,
        string? gid = null,
        Action<DtmGlobalTransactionOptions>? configure = null,
        CancellationToken cancellationToken = default);

    Task<TResult> ExecuteSagaAsync<TResult>(
        Func<ISagaTransactionContext, Task<TResult>> action,
        string? gid = null,
        Action<DtmGlobalTransactionOptions>? configure = null,
        CancellationToken cancellationToken = default);
}
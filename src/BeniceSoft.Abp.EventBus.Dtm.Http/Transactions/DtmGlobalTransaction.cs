using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public class DtmGlobalTransaction : IGlobalTransaction, ITransientDependency
{
    private readonly ITccGlobalTransactionManager _tccGlobalTransactionManager;
    private readonly ISagaGlobalTransactionManager _sagaGlobalTransactionManager;

    public DtmGlobalTransaction(
        ITccGlobalTransactionManager tccGlobalTransactionManager,
        ISagaGlobalTransactionManager sagaGlobalTransactionManager)
    {
        _tccGlobalTransactionManager = tccGlobalTransactionManager;
        _sagaGlobalTransactionManager = sagaGlobalTransactionManager;
    }

    public Task ExecuteTccAsync(
        Func<ITccTransactionContext, Task> action,
        string? gid = null,
        Action<DtmGlobalTransactionOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        return _tccGlobalTransactionManager.ExecuteAsync(action, gid, configure, cancellationToken);
    }

    public Task<TResult> ExecuteTccAsync<TResult>(
        Func<ITccTransactionContext, Task<TResult>> action,
        string? gid = null,
        Action<DtmGlobalTransactionOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        return _tccGlobalTransactionManager.ExecuteAsync(action, gid, configure, cancellationToken);
    }

    public Task ExecuteSagaAsync(
        Func<ISagaTransactionContext, Task> action,
        string? gid = null,
        Action<DtmGlobalTransactionOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        return _sagaGlobalTransactionManager.ExecuteAsync(action, gid, configure, cancellationToken);
    }

    public Task<TResult> ExecuteSagaAsync<TResult>(
        Func<ISagaTransactionContext, Task<TResult>> action,
        string? gid = null,
        Action<DtmGlobalTransactionOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        return _sagaGlobalTransactionManager.ExecuteAsync(action, gid, configure, cancellationToken);
    }
}

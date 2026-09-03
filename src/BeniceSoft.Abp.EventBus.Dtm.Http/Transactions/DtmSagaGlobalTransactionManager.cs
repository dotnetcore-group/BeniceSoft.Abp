using Dtmcli;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public class DtmSagaGlobalTransactionManager : ISagaGlobalTransactionManager, ITransientDependency
{
    private readonly IDtmTransFactory _dtmTransFactory;
    private readonly IDtmGidProvider _gidProvider;
    private readonly DtmGlobalTransactionDefaults _defaults;
    private readonly DtmHttpOptions _dtmHttpOptions;
    private readonly DtmTransactionCallbackOptions _callbackOptions;
    private readonly IDtmRequestHeadersBuilder? _dtmMessageBuilder;

    public DtmSagaGlobalTransactionManager(
        IDtmTransFactory dtmTransFactory,
        IDtmGidProvider gidProvider,
        IOptions<DtmGlobalTransactionDefaults> defaults,
        IOptions<DtmHttpOptions> dtmHttpOptions,
        IOptions<DtmTransactionCallbackOptions> callbackOptions,
        IDtmRequestHeadersBuilder? dtmMessageBuilder = null)
    {
        _dtmTransFactory = dtmTransFactory;
        _gidProvider = gidProvider;
        _defaults = defaults.Value;
        _dtmHttpOptions = dtmHttpOptions.Value;
        _callbackOptions = callbackOptions.Value;
        _dtmMessageBuilder = dtmMessageBuilder;
    }

    public Task ExecuteAsync(
        Func<ISagaTransactionContext, Task> action,
        string? gid = null,
        Action<DtmGlobalTransactionOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync<object?>(
            async context =>
            {
                await action(context);
                return null;
            },
            gid,
            configure,
            cancellationToken);
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<ISagaTransactionContext, Task<TResult>> action,
        string? gid = null,
        Action<DtmGlobalTransactionOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var transactionId = string.IsNullOrWhiteSpace(gid) ? _gidProvider.Create() : gid;
        var saga = _dtmTransFactory.NewSaga(transactionId!);
        
        var options = BuildOptions(configure);
        ApplyCommonOptions(saga, options);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (_dtmMessageBuilder is not null)
        {
            await _dtmMessageBuilder.BuildHeadersAsync(headers);
        }

        foreach (var pair in options.BranchHeaders)
        {
            headers[pair.Key] = pair.Value;
        }

        saga.SetBranchHeaders(headers);

        var context = new SagaTransactionContext(
            transactionId!,
            saga,
            _dtmHttpOptions.AppUrl,
            _callbackOptions.SagaCallbackPathPrefix);

        cancellationToken.ThrowIfCancellationRequested();
        var result = await action(context);

        await context.SubmitAsync(cancellationToken);

        return result;
    }

    private DtmGlobalTransactionOptions BuildOptions(Action<DtmGlobalTransactionOptions>? configure)
    {
        var options = new DtmGlobalTransactionOptions
        {
            EnableWaitResult = _defaults.EnableWaitResult,
            TimeoutToFail = _defaults.TimeoutToFail,
            RetryInterval = _defaults.RetryInterval,
            RetryLimit = _defaults.RetryLimit
        };

        configure?.Invoke(options);

        return options;
    }

    private static void ApplyCommonOptions(Saga saga, DtmGlobalTransactionOptions options)
    {
        if (options.EnableWaitResult)
        {
            saga.EnableWaitResult();
        }

        if (options.TimeoutToFail > 0)
        {
            saga.SetTimeoutToFail(options.TimeoutToFail);
        }

        if (options.RetryInterval > 0)
        {
            saga.SetRetryInterval(options.RetryInterval);
        }

        if (options.RetryLimit > 0)
        {
            saga.SetRetryLimit(options.RetryLimit);
        }

    }
}


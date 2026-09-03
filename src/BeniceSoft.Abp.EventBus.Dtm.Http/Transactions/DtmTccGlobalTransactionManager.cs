using Dtmcli;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public class DtmTccGlobalTransactionManager : ITccGlobalTransactionManager, ITransientDependency
{
    private readonly IDtmTransFactory _dtmTransFactory;
    private readonly IDtmGidProvider _gidProvider;
    private readonly DtmGlobalTransactionDefaults _defaults;
    private readonly DtmHttpOptions _dtmHttpOptions;
    private readonly DtmTransactionCallbackOptions _callbackOptions;
    private readonly IDtmRequestHeadersBuilder? _dtmMessageBuilder;

    public DtmTccGlobalTransactionManager(
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
        Func<ITccTransactionContext, Task> action,
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
        Func<ITccTransactionContext, Task<TResult>> action,
        string? gid = null,
        Action<DtmGlobalTransactionOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var transactionId = string.IsNullOrWhiteSpace(gid) ? _gidProvider.Create() : gid;
        var tcc = _dtmTransFactory.NewTcc(transactionId!);

        var options = BuildOptions(configure);
        ApplyCommonOptions(tcc, options);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (_dtmMessageBuilder is not null)
        {
            await _dtmMessageBuilder.BuildHeadersAsync(headers);
        }

        foreach (var pair in options.BranchHeaders)
        {
            headers[pair.Key] = pair.Value;
        }

        tcc.SetBranchHeaders(headers);

        var context = new TccTransactionContext(
            transactionId!,
            tcc,
            headers,
            _dtmHttpOptions.AppUrl,
            _callbackOptions.TccCallbackPathPrefix);

        cancellationToken.ThrowIfCancellationRequested();
        var result = await action(context);

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

    private static void ApplyCommonOptions(Tcc tcc, DtmGlobalTransactionOptions options)
    {
        if (options.EnableWaitResult)
        {
            tcc.EnableWaitResult();
        }

        if (options.TimeoutToFail > 0)
        {
            tcc.SetTimeoutToFail(options.TimeoutToFail);
        }

        if (options.RetryInterval > 0)
        {
            tcc.SetRetryInterval(options.RetryInterval);
        }
    }
}

using Dtmcli;
using DtmCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public class DtmTccGlobalTransactionManager : ITccGlobalTransactionManager, ITransientDependency
{
    private readonly IDtmTransFactory _dtmTransFactory;
    private readonly IDtmClient _dtmClient;
    private readonly IDtmGidProvider _gidProvider;
    private readonly ILogger<DtmTccGlobalTransactionManager> _logger;
    private readonly DtmGlobalTransactionDefaults _defaults;
    private readonly DtmHttpOptions _dtmHttpOptions;
    private readonly DtmTransactionCallbackOptions _callbackOptions;
    private readonly IDtmRequestHeadersBuilder? _dtmMessageBuilder;

    public DtmTccGlobalTransactionManager(
        IDtmTransFactory dtmTransFactory,
        IDtmClient dtmClient,
        IDtmGidProvider gidProvider,
        ILogger<DtmTccGlobalTransactionManager> logger,
        IOptions<DtmGlobalTransactionDefaults> defaults,
        IOptions<DtmHttpOptions> dtmHttpOptions,
        IOptions<DtmTransactionCallbackOptions> callbackOptions,
        IDtmRequestHeadersBuilder? dtmMessageBuilder = null)
    {
        _dtmTransFactory = dtmTransFactory;
        _dtmClient = dtmClient;
        _gidProvider = gidProvider;
        _logger = logger;
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
        ArgumentNullException.ThrowIfNull(action);

        var transactionId = string.IsNullOrWhiteSpace(gid) ? _gidProvider.Create() : gid;
        var transaction = TransBase.NewTransBase(transactionId!, Constant.TYPE_TCC, "", "");
        var tcc = _dtmTransFactory.NewTcc(transaction);

        var options = BuildOptions(configure);
        ApplyCommonOptions(tcc, options);
        if (options.RetryLimit > 0)
        {
            transaction.RetryLimit = options.RetryLimit;
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (_dtmMessageBuilder is not null)
        {
            await _dtmMessageBuilder.BuildHeadersAsync(headers);
        }

        foreach (var pair in options.BranchHeaders)
        {
            headers[pair.Key] = pair.Value;
        }

        headers.Remove(DtmRequestHeaderNames.DbContextType);
        headers.Remove(DtmRequestHeaderNames.HashedConnectionString);

        tcc.SetBranchHeaders(headers);

        var context = new TccTransactionContext(
            transactionId!,
            tcc,
            headers,
            _dtmHttpOptions.AppUrl,
            _callbackOptions.TccCallbackPathPrefix);

        cancellationToken.ThrowIfCancellationRequested();
        TResult result;
        try
        {
            await _dtmClient.TransCallDtm(transaction, transaction, "prepare", cancellationToken);
            result = await action(context);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception exception)
        {
            transaction.RollbackReason = exception.Message.Length > 1023
                ? exception.Message[..1023]
                : exception.Message;

            try
            {
                using var abortCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(
                    _dtmHttpOptions.Timeout > 0 ? _dtmHttpOptions.Timeout : 30 * 1000));
                await _dtmClient.TransCallDtm(transaction, transaction, "abort", abortCancellation.Token);
            }
            catch (Exception abortException)
            {
                exception.Data["DtmAbortException"] = abortException;
                _logger.LogError(abortException, "TCC 终止请求失败，Gid={Gid}", transactionId);
            }

            throw;
        }

        try
        {
            // submit 发出后可能已经被 DTM 接收，响应异常时保留协调器的事务决定
            await _dtmClient.TransCallDtm(transaction, transaction, "submit", cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "TCC 提交结果未确认，请按 Gid 查询 DTM 事务状态，Gid={Gid}", transactionId);
            throw;
        }

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

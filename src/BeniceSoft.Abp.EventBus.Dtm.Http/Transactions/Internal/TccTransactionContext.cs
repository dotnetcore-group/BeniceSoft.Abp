using Dtmcli;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

internal sealed class TccTransactionContext : ITccTransactionContext
{
    private readonly Tcc _tcc;
    private readonly IReadOnlyDictionary<string, string> _defaultHeaders;
    private readonly string _appUrl;
    private readonly string _tccCallbackPathPrefix;

    public string Gid { get; }

    public TccTransactionContext(
        string gid,
        Tcc tcc,
        IReadOnlyDictionary<string, string> defaultHeaders,
        string appUrl,
        string tccCallbackPathPrefix)
    {
        Gid = gid;
        _tcc = tcc;
        _defaultHeaders = defaultHeaders;
        _appUrl = appUrl.TrimEnd('/');
        _tccCallbackPathPrefix = tccCallbackPathPrefix.EnsureStartsWith('/').TrimEnd('/');
    }

    private async Task CallBranchByUrlsAsync(
        string tryUrl,
        string confirmUrl,
        string cancelUrl,
        object body,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var mergedHeaders = new Dictionary<string, string>(_defaultHeaders, StringComparer.OrdinalIgnoreCase);

        if (headers is not null)
        {
            foreach (var pair in headers)
            {
                mergedHeaders[pair.Key] = pair.Value;
            }
        }

        _tcc.SetBranchHeaders(mergedHeaders);


        var resolvedTryUrl = ResolveBranchUrl(tryUrl);
        var resolvedConfirmUrl = ResolveBranchUrl(confirmUrl);
        var resolvedCancelUrl = ResolveBranchUrl(cancelUrl);

        await _tcc.CallBranch(body, resolvedTryUrl, resolvedConfirmUrl, resolvedCancelUrl, cancellationToken);
    }

    private Task CallBranchByHandlerAsync(
        string serviceName,
        string handlerName,
        object body,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(handlerName))
        {
            throw new ArgumentException("handlerName 不能为空。", nameof(handlerName));
        }

        var servicePath = string.IsNullOrWhiteSpace(serviceName)
            ? string.Empty
            : serviceName.StartsWith('/') ? serviceName : $"/{serviceName}";

        servicePath = servicePath.TrimEnd('/');
        var normalizedHandlerName = handlerName.Trim('/');

        var tryUrl = $"{servicePath}{_tccCallbackPathPrefix}/{normalizedHandlerName}/try";
        var confirmUrl = $"{servicePath}{_tccCallbackPathPrefix}/{normalizedHandlerName}/confirm";
        var cancelUrl = $"{servicePath}{_tccCallbackPathPrefix}/{normalizedHandlerName}/cancel";

        return CallBranchByUrlsAsync(tryUrl, confirmUrl, cancelUrl, body, headers, cancellationToken);
    }

    public Task CallBranchAsync<TRequest>(
        TRequest body,
        string? serviceName = null,
        string? handlerName = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
        where TRequest : class, IBranchRequest
    {
        ArgumentNullException.ThrowIfNull(body);

        var metadata = DtmBranchMetadataResolver.Resolve(typeof(TRequest));
        var resolvedServiceName = string.IsNullOrWhiteSpace(serviceName) ? metadata.ServiceName : serviceName;
        var resolvedHandlerName = string.IsNullOrWhiteSpace(handlerName) ? metadata.HandlerName : handlerName;

        return CallBranchByHandlerAsync(resolvedServiceName, resolvedHandlerName, body, headers, cancellationToken);
    }

    private string ResolveBranchUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return url;
        }

        if (string.IsNullOrWhiteSpace(_appUrl))
        {
            throw new InvalidOperationException("DtmHttpOptions.AppUrl 未配置，无法拼接分支相对地址。");
        }

        return url.StartsWith('/') ? $"{_appUrl}{url}" : $"{_appUrl}/{url}";
    }
}
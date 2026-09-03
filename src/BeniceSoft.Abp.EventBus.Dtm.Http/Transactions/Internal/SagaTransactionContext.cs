using Dtmcli;


namespace BeniceSoft.Abp.EventBus.Dtm.Http;

internal sealed class SagaTransactionContext : ISagaTransactionContext
{
    private readonly Saga _saga;
    private readonly string _appUrl;
    private readonly string _sagaCallbackPathPrefix;

    public string Gid { get; }

    public SagaTransactionContext(
        string gid,
        Saga saga,
        string appUrl,
        string sagaCallbackPathPrefix)
    {
        Gid = gid;
        _saga = saga;
        _appUrl = appUrl.TrimEnd('/');
        _sagaCallbackPathPrefix = sagaCallbackPathPrefix.EnsureStartsWith('/').TrimEnd('/');
    }

    public void AddBranch(string actionUrl, string compensateUrl, object body)
    {
        _saga.Add(
            ResolveBranchUrl(actionUrl),
            ResolveBranchUrl(compensateUrl),
            body);
    }

    public void AddBranchByHandler(string serviceName, string handlerName, object body)
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

        var actionUrl = $"{servicePath}{_sagaCallbackPathPrefix}/{normalizedHandlerName}/action";
        var compensateUrl = $"{servicePath}{_sagaCallbackPathPrefix}/{normalizedHandlerName}/compensate";

        AddBranch(actionUrl, compensateUrl, body);
    }

    public void AddBranchByHandler<TRequest>(
        TRequest body,
        string? serviceName = null,
        string? handlerName = null)
        where TRequest : class, IBranchRequest
    {
        ArgumentNullException.ThrowIfNull(body);

        var metadata = DtmBranchMetadataResolver.Resolve(typeof(TRequest));
        var resolvedServiceName = string.IsNullOrWhiteSpace(serviceName) ? metadata.ServiceName : serviceName;
        var resolvedHandlerName = string.IsNullOrWhiteSpace(handlerName) ? metadata.HandlerName : handlerName;

        AddBranchByHandler(resolvedServiceName, resolvedHandlerName, body);
    }

    public Task SubmitAsync(CancellationToken cancellationToken = default)
    {
        return _saga.Submit(cancellationToken);
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
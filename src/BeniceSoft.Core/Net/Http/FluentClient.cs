namespace BeniceSoft.Http.FluentClient;

public class FluentClient : IFluentClient
{
    private readonly List<Func<IHttpRequest, IHttpRequest>> _defaults = [];
    private readonly bool _manageBaseClient;
    private HttpCompletionOption _completion = HttpCompletionOption.ResponseContentRead;
    private IHttpSerializer _serializer = new JsonHttpSerializer();
    private bool _disposed;

    public HttpClient BaseClient { get; }
    public Uri BaseUrl { get; }
    public ICollection<IHttpFilter> Filters { get; } = [];
    public IRequestCoordinator? RequestCoordinator { get; private set; }

    public FluentClient(HttpClient client, Uri uri, bool manageBaseClient = true)
    {
        BaseClient = client ?? throw new ArgumentNullException(nameof(client));
        BaseUrl = uri ?? throw new ArgumentNullException(nameof(uri));
        _manageBaseClient = manageBaseClient;
    }

    public FluentClient(HttpClient client, string url, bool manageBaseClient = true)
        : this(client, new Uri(url), manageBaseClient)
    {
    }

    public IFluentClient AddDefault(Func<IHttpRequest, IHttpRequest> apply)
    {
        _defaults.Add(apply);
        return this;
    }

    public IHttpRequest Send(HttpRequestMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var request = new HttpRequest(BaseClient, message, Filters)
            .WithRequestCoordinator(RequestCoordinator)
            .WithSerializer(_serializer)
            .WithCompletionOption(_completion);

        foreach (var apply in _defaults)
        {
            request = apply?.Invoke(request) ?? request;
        }

        return request;
    }

    public IFluentClient SetRequestCoordinator(IRequestCoordinator? requestCoordinator)
    {
        RequestCoordinator = requestCoordinator;
        return this;
    }

    public IFluentClient SetCompletionOption(HttpCompletionOption option)
    {
        _completion = option;
        return this;
    }

    public IFluentClient SetHttpSerializer(IHttpSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        _serializer = serializer;
        return this;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_manageBaseClient)
        {
            BaseClient.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}

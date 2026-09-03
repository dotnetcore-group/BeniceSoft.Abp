namespace BeniceSoft.Http.FluentClient;

public interface IFluentClient : IDisposable
{
    /// <summary>
    /// the underlying HTTP client.
    /// </summary>
    HttpClient BaseClient { get; }

    /// <summary>
    /// the base url.
    /// </summary>
    Uri BaseUrl { get; }

    /// <summary>
    /// interceptors which can read and modify HTTP requests and responses.
    /// </summary>
    ICollection<IHttpFilter> Filters { get; }

    /// <summary>
    /// create an asynchronous HTTP request message (but don't dispatch it yet).
    /// </summary>
    IHttpRequest Send(HttpRequestMessage message);

    /// <summary>
    /// set the default request coordinator.
    /// </summary>
    IFluentClient SetRequestCoordinator(IRequestCoordinator? requestCoordinator);

    /// <summary>
    /// add a default behaviour for all subsequent HTTP requests.
    /// </summary>
    IFluentClient AddDefault(Func<IHttpRequest, IHttpRequest> apply);

    IFluentClient SetCompletionOption(HttpCompletionOption option);

    IFluentClient SetHttpSerializer(IHttpSerializer serializer);
}

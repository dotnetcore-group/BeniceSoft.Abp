using System.Net.Http.Headers;
using System.Text;

namespace BeniceSoft.Http.FluentClient;

/// <summary>
/// builds and dispatches an asynchronous HTTP request, and asynchronously parses the response.
/// </summary>
public interface IHttpRequest
{
    HttpRequestMessage Message { get; }
    CancellationToken CancellationToken { get; }
    ICollection<IHttpFilter> Filters { get; }
    RequestOptions Options { get; }
    IRequestCoordinator? RequestCoordinator { get; }
    HttpCompletionOption CompletionOption { get; }
    IHttpSerializer Serializer { get; }

    IHttpRequest WithCancellationToken(CancellationToken cancellationToken);
    IHttpRequest WithOptions(RequestOptions options);
    IHttpRequest WithRequestCoordinator(IRequestCoordinator? requestCoordinator);
    IHttpRequest WithCompletionOption(HttpCompletionOption option);
    IHttpRequest WithSerializer(IHttpSerializer serializer);
    IHttpRequest WithBody<T>(T body);
    Task<IHttpResponse> AsResponse();
}

public class RequestOptions
{
    public bool IsAjax { get; set; }
    public string? Referer { get; set; }
    public string? Accept { get; set; }
    public string? UserAgent { get; set; }
    public bool IgnoreNullArguments { get; set; }
    public bool IgnoreHttpErrors { get; set; } = true;
}

internal sealed class HttpRequest : IHttpRequest
{
    private readonly HttpClient _client;

    public HttpRequestMessage Message { get; }
    public CancellationToken CancellationToken { get; private set; } = CancellationToken.None;
    public ICollection<IHttpFilter> Filters { get; }
    public RequestOptions Options { get; private set; } = new();
    public IRequestCoordinator? RequestCoordinator { get; private set; }
    public HttpCompletionOption CompletionOption { get; private set; }
    public IHttpSerializer Serializer { get; private set; } = new JsonHttpSerializer();

    public HttpRequest(HttpClient client, HttpRequestMessage message, ICollection<IHttpFilter> filters)
    {
        _client = client;
        Message = message;
        Filters = filters;
    }

    public async Task<IHttpResponse> AsResponse()
    {
        // apply request filters
        foreach (var filter in Filters)
        {
            await filter.OnRequestAsync(this).ConfigureAwait(false);
        }

        Task<HttpResponseMessage> SendImplAsync(IHttpRequest request)
        {
            var headers = request.Message.Headers;

            if (Options.IsAjax)
                headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");

            if (!string.IsNullOrWhiteSpace(Options.Referer))
                headers.TryAddWithoutValidation("Referer", Options.Referer);

            if (!string.IsNullOrWhiteSpace(Options.Accept))
                headers.TryAddWithoutValidation("Accept", Options.Accept);

            if (!string.IsNullOrWhiteSpace(Options.UserAgent))
            {
                headers.Remove("User-Agent");
                headers.TryAddWithoutValidation("User-Agent", Options.UserAgent);
            }

            return _client.SendAsync(request.Message, request.CompletionOption, request.CancellationToken);
        }

        var responseMessage = RequestCoordinator != null
            ? await RequestCoordinator.ExecuteAsync(this, SendImplAsync)
            : await SendImplAsync(this);

        var response = new HttpResponse(responseMessage, Serializer, CancellationToken);

        // apply response filters
        foreach (var filter in Filters)
        {
            await filter.OnResponseAsync(response).ConfigureAwait(false);
        }

        if (!Options.IgnoreHttpErrors && !response.Message.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"the API query failed with status code {response.Message.StatusCode}: {response.Message.ReasonPhrase}");
        }

        return response;
    }

    public IHttpRequest WithCancellationToken(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
        return this;
    }

    public IHttpRequest WithOptions(RequestOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        return this;
    }

    public IHttpRequest WithRequestCoordinator(IRequestCoordinator? requestCoordinator)
    {
        RequestCoordinator = requestCoordinator;
        return this;
    }

    public IHttpRequest WithBody<T>(T body)
    {
        Message.Content = body switch
        {
            null => null,
            HttpContent content => content,
            string json => new StringContent(json, Encoding.UTF8,
                new MediaTypeHeaderValue(MimeTypes.Application.Json, "utf-8")),
            _ => Serializer.Build(body)
        };
        return this;
    }

    public IHttpRequest WithCompletionOption(HttpCompletionOption option)
    {
        CompletionOption = option;
        return this;
    }

    public IHttpRequest WithSerializer(IHttpSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        Serializer = serializer;
        return this;
    }
}

using System.Net;

namespace BeniceSoft.Http.FluentClient;

/// <summary>
/// asynchronously parses an HTTP response.
/// </summary>
public interface IHttpResponse
{
    bool IsSuccessStatusCode { get; }
    HttpStatusCode Status { get; }
    HttpResponseMessage Message { get; }
    Task<byte[]> AsByteArray();
    Task<string> AsString();
    Task<Stream> AsStream();
    Task<T?> As<T>();
}

internal sealed class HttpResponse : IHttpResponse
{
    private readonly IHttpSerializer _serializer;
    private readonly CancellationToken _cancellationToken;

    public bool IsSuccessStatusCode => Message.IsSuccessStatusCode;
    public HttpStatusCode Status => Message.StatusCode;
    public HttpResponseMessage Message { get; }

    public HttpResponse(HttpResponseMessage message, IHttpSerializer serializer, CancellationToken cancellationToken)
    {
        Message = message;
        _serializer = serializer;
        _cancellationToken = cancellationToken;
    }

    public Task<byte[]> AsByteArray() => Message.Content.ReadAsByteArrayAsync(_cancellationToken);

    public Task<string> AsString() => Message.Content.ReadAsStringAsync(_cancellationToken);

    public Task<Stream> AsStream() => Message.Content.ReadAsStreamAsync(_cancellationToken);

    public Task<T?> As<T>() => _serializer.ReadAsync<T>(Message.Content, _cancellationToken);
}

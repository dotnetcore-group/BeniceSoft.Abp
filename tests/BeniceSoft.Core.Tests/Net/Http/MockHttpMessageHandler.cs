using System.Net;

namespace BeniceSoft.Core.Tests.Net.Http;

/// <summary>
/// Mock HttpMessageHandler for testing.
/// </summary>
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private HttpResponseMessage _response = new(HttpStatusCode.OK);
    private readonly List<HttpRequestMessage> _requests = [];
    private Func<HttpRequestMessage, HttpResponseMessage>? _responseFactory;

    public HttpRequestMessage? LastRequest => _requests.Count > 0 ? _requests[^1] : null;
    public IReadOnlyList<HttpRequestMessage> AllRequests => _requests;
    public int RequestCount => _requests.Count;

    public void SetResponse(HttpResponseMessage response)
    {
        _response = response;
    }

    public void SetResponse(HttpStatusCode statusCode, string? content = null)
    {
        _response = new HttpResponseMessage(statusCode);
        if (content != null)
        {
            _response.Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json");
        }
    }

    public void SetResponseFactory(Func<HttpRequestMessage, HttpResponseMessage> factory)
    {
        _responseFactory = factory;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Add(request);
        var response = _responseFactory?.Invoke(request) ?? _response;
        return Task.FromResult(response);
    }
}

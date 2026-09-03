using BeniceSoft.Http.FluentClient;

namespace BeniceSoft.Core.Tests.Net.Http;

/// <summary>
/// Simple IHttpFilter for testing.
/// </summary>
internal sealed class TestFilter : IHttpFilter
{
    private readonly Action<IHttpRequest>? _onRequest;
    private readonly Action<IHttpResponse>? _onResponse;

    public TestFilter(Action<IHttpRequest>? onRequest = null, Action<IHttpResponse>? onResponse = null)
    {
        _onRequest = onRequest;
        _onResponse = onResponse;
    }

    public Task OnRequestAsync(IHttpRequest request)
    {
        _onRequest?.Invoke(request);
        return Task.CompletedTask;
    }

    public Task OnResponseAsync(IHttpResponse response)
    {
        _onResponse?.Invoke(response);
        return Task.CompletedTask;
    }
}

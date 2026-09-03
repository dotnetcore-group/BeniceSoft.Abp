namespace BeniceSoft.Http.FluentClient;

/// <summary>
/// a middleware class which can intercept and modify HTTP requests and responses.
/// This can be used to implement common authentication, error-handling, etc.
/// </summary>
public interface IHttpFilter
{
    /// <summary>
    /// method invoked just before the HTTP request is submitted.
    /// This method can modify the outgoing HTTP request.
    /// </summary>
    /// <param name="request">the HTTP request.</param>
    Task OnRequestAsync(IHttpRequest request);

    /// <summary>
    /// method invoked just after the HTTP response is received.
    /// This method can modify the incoming HTTP response.
    /// </summary>
    /// <param name="response">the HTTP response.</param>
    Task OnResponseAsync(IHttpResponse response);
}

using System.Collections.Specialized;

namespace BeniceSoft.Http.FluentClient;

/// <summary>
/// constructs HTTP request bodies.
/// </summary>
public interface IBodyBuilder
{
    /// <summary>
    /// get a form URL-encoded body.
    /// </summary>
    HttpContent FormUrlEncoded(IEnumerable<KeyValuePair<string, object?>> arguments);

    /// <summary>
    /// get a form data body.
    /// </summary>
    HttpContent FormData(NameValueCollection arguments);

    /// <summary>
    /// get a file upload body (using multi-part form data).
    /// </summary>
    HttpContent FileUpload(IEnumerable<KeyValuePair<string, Stream>> files);
}

internal sealed class BodyBuilder : IBodyBuilder
{
    private readonly IHttpRequest _request;

    public BodyBuilder(IHttpRequest request)
    {
        _request = request;
    }

    public HttpContent FormUrlEncoded(IEnumerable<KeyValuePair<string, object?>> arguments)
    {
        var pairs = from p in arguments
                    let val = p.Value?.ToString() ?? string.Empty
                    where !_request.Options.IgnoreNullArguments || !string.IsNullOrWhiteSpace(val)
                    select new KeyValuePair<string, string>(p.Key, val);
        return new FormUrlEncodedContent(pairs);
    }

    public HttpContent FormData(NameValueCollection arguments)
    {
        var content = new MultipartFormDataContent();

        foreach (var key in arguments.AllKeys)
        {
            var value = arguments[key];
            if (!_request.Options.IgnoreNullArguments || !string.IsNullOrWhiteSpace(value))
            {
                content.Add(new StringContent(value ?? string.Empty, System.Text.Encoding.UTF8), key!);
            }
        }

        return content;
    }

    public HttpContent FileUpload(IEnumerable<KeyValuePair<string, Stream>> files)
    {
        var content = new MultipartFormDataContent();

        foreach (var file in files)
        {
            var streamContent = new StreamContent(file.Value);
            content.Add(streamContent, file.Key, file.Key);
        }

        return content;
    }
}

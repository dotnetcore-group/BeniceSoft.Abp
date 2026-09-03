using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using BeniceSoft.Core;

namespace BeniceSoft.Http.FluentClient;

/// <summary>
/// extension methods for FluentClient chain-style HTTP API.
/// </summary>
public static class FluentClientExtensions
{
    #region IFluentClient Extensions

    public static IFluentClient SetTimeout(this IFluentClient client, TimeSpan timeout)
    {
        client.BaseClient.Timeout = timeout;
        return client;
    }

    public static IHttpRequest Send(this IFluentClient client, HttpMethod method, string resource = "")
    {
        var uri = ResolveFinalUrl(client.BaseUrl, resource);
        var request = new HttpRequestMessage(method, uri);
        return client.Send(request);
    }

    public static IHttpRequest Get(this IFluentClient client, string resource = "")
        => client.Send(HttpMethod.Get, resource);

    public static IHttpRequest Delete(this IFluentClient client, string resource = "")
        => client.Send(HttpMethod.Delete, resource);

    public static IHttpRequest Post<TBody>(this IFluentClient client, TBody body, string resource = "")
        => client.Send(HttpMethod.Post, resource).WithBody(body);

    public static IHttpRequest Put<TBody>(this IFluentClient client, TBody body, string resource = "")
        => client.Send(HttpMethod.Put, resource).WithBody(body);

    public static async Task DownloadFileAsync(this IFluentClient client, string filePath, CancellationToken cancellationToken = default)
    {
        var bytes = await client.BaseClient.GetByteArrayAsync(client.BaseUrl, cancellationToken);
        await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
    }

    #endregion

    #region IHttpRequest Extensions

    public static IHttpRequest WithArgument(this IHttpRequest request, string key, object? value)
    {
        request.Message.RequestUri = WithArguments(request.Message.RequestUri!,
            request.Options.IgnoreNullArguments,
            new KeyValuePair<string, object?>(key, value));
        return request;
    }

    public static IHttpRequest WithArguments<TKey, TValue>(this IHttpRequest request, IEnumerable<KeyValuePair<TKey, TValue>> arguments)
    {
        if (arguments == null) return request;

        var args = (from arg in arguments
                    let key = arg.Key?.ToString()
                    where !string.IsNullOrWhiteSpace(key)
                    select new KeyValuePair<string, object?>(key!, arg.Value)).ToArray();

        request.Message.RequestUri = WithArguments(request.Message.RequestUri!,
            request.Options.IgnoreNullArguments, args);
        return request;
    }

    public static IHttpRequest WithArguments(this IHttpRequest request, object? arguments)
    {
        if (arguments == null) return request;

        var args = arguments.GetType().GetRuntimeProperties()
            .Where(t => t.CanRead && t.GetIndexParameters().Length == 0)
            .Select(t => new KeyValuePair<string, object?>(t.Name, t.GetValue(arguments)))
            .ToArray();

        request.Message.RequestUri = WithArguments(request.Message.RequestUri!,
            request.Options.IgnoreNullArguments, args);
        return request;
    }

    public static IHttpRequest WithHeader(this IHttpRequest request, string key, string value)
    {
        request.Message.Headers.TryAddWithoutValidation(key, value);
        return request;
    }

    public static IHttpRequest WithHeaders<TKey, TValue>(this IHttpRequest request, IEnumerable<KeyValuePair<TKey, TValue>>? headers)
    {
        if (headers == null) return request;

        foreach (var header in headers)
        {
            var key = header.Key?.ToString();
            if (!string.IsNullOrWhiteSpace(key))
            {
                request.Message.Headers.TryAddWithoutValidation(key!, header.Value?.ToString() ?? string.Empty);
            }
        }

        return request;
    }

    public static IHttpRequest WithBody(this IHttpRequest request, Func<IBodyBuilder, HttpContent> bodyBuilder)
    {
        request.Message.Content = bodyBuilder(new BodyBuilder(request));
        return request;
    }

    public static IHttpRequest WithJsonBody<T>(this IHttpRequest request, T body, JsonSerializerOptions? options = null, MediaTypeHeaderValue? mediaType = null)
    {
        request.Message.Content = JsonContent.Create(body, mediaType, options);
        return request;
    }

    public static async Task<HttpResponseMessage> AsMessage(this IHttpRequest request)
    {
        var response = await request.AsResponse().ConfigureAwait(false);
        return response.Message;
    }

    public static async Task<string> AsString(this IHttpRequest request)
    {
        var response = await request.AsResponse().ConfigureAwait(false);
        return await response.AsString();
    }

    public static async Task<byte[]> AsByteArray(this IHttpRequest request)
    {
        var response = await request.AsResponse().ConfigureAwait(false);
        return await response.AsByteArray();
    }

    public static async Task<Stream> AsStream(this IHttpRequest request)
    {
        var response = await request.AsResponse().ConfigureAwait(false);
        return await response.AsStream();
    }

    public static async Task<T?> As<T>(this IHttpRequest request)
    {
        var response = await request.AsResponse().ConfigureAwait(false);
        return await response.As<T>();
    }

    public static async Task<T?> AsJson<T>(this IHttpRequest request, JsonSerializerOptions? options = null)
    {
        var response = await request.AsResponse().ConfigureAwait(false);
        return await response.Message.Content.ReadFromJsonAsync<T>(options, request.CancellationToken);
    }

    public static async Task DownloadFileAsync(this IHttpRequest request, string filePath)
    {
        var bytes = await request.AsByteArray();
        await File.WriteAllBytesAsync(filePath, bytes, request.CancellationToken);
    }

    #endregion

    #region IBodyBuilder Extensions

    public static HttpContent FormUrlEncoded(this IBodyBuilder bodyBuilder, object arguments)
    {
        return bodyBuilder.FormUrlEncoded(GetKeyValueArguments(arguments));
    }

    public static HttpContent FormUrlEncoded(this IBodyBuilder bodyBuilder, IDictionary<string, object?> arguments)
    {
        return bodyBuilder.FormUrlEncoded([.. arguments]);
    }

    public static HttpContent FileUpload(this IBodyBuilder bodyBuilder, string fullPath)
    {
        return bodyBuilder.FileUpload(new FileInfo(fullPath));
    }

    public static HttpContent FileUpload(this IBodyBuilder bodyBuilder, params FileInfo[] files)
    {
        return bodyBuilder.FileUpload(files.Select(file =>
            file.Exists
                ? new KeyValuePair<string, Stream>(file.Name, file.OpenRead())
                : throw new FileNotFoundException($"there's no file matching path '{file.FullName}'.")));
    }

    #endregion

    #region HttpRequestHeaders Extensions

    public static HttpRequestHeaders SetAuthentication(this HttpRequestHeaders headers, string scheme, string parameter)
    {
        headers.Authorization = new AuthenticationHeaderValue(scheme, parameter);
        return headers;
    }

    public static HttpRequestHeaders SetBasicAuthentication(this HttpRequestHeaders headers, string username, string password)
    {
        return headers.SetAuthentication("Basic",
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}")));
    }

    public static HttpRequestHeaders SetBearerAuthentication(this HttpRequestHeaders headers, string token)
    {
        return headers.SetAuthentication("Bearer", token);
    }

    #endregion

    #region ResponseResult Extensions

    /// <summary>
    /// Deserialize the response as ResponseResult&lt;T&gt; and unwrap the data.
    /// Throws ApiException if the response indicates failure.
    /// </summary>
    public static async Task<T?> AsApi<T>(this IHttpRequest request)
    {
        var response = await request.AsResponse().ConfigureAwait(false);
        var result = await response.As<ResponseResult<T>>();

        if (result is { IsSuccess: true })
            return result.Data;

        throw new ApiException(result?.Code ?? -1, result?.Message ?? "请求失败");
    }

    #endregion

    #region Private Helpers

    private static Uri WithArguments(Uri uri, bool ignoreNullArguments, params KeyValuePair<string, object?>[] arguments)
    {
        var newQueryString = string.Join("&",
            from argument in arguments
            where !ignoreNullArguments || argument.Value != null
            let key = WebUtility.UrlEncode(argument.Key)
            let value = argument.Value != null ? WebUtility.UrlEncode(argument.Value.ToString()) : string.Empty
            select key + "=" + value);

        if (string.IsNullOrWhiteSpace(newQueryString)) return uri;

        var builder = new UriBuilder(uri);
        builder.Query = !string.IsNullOrWhiteSpace(builder.Query)
            ? builder.Query.TrimStart('?') + "&" + newQueryString
            : newQueryString;

        return builder.Uri;
    }

    private static Uri ResolveFinalUrl(Uri baseUrl, string resource)
    {
        if (string.IsNullOrWhiteSpace(resource)) return baseUrl;

        if (Uri.TryCreate(resource, UriKind.Absolute, out var absoluteUrl)) return absoluteUrl;

        resource = resource.Trim();
        var builder = new UriBuilder(baseUrl);

        // fragment
        if (!string.IsNullOrWhiteSpace(builder.Fragment) || resource.StartsWith('#'))
            return new Uri(baseUrl + resource);

        // query string
        if (resource.StartsWith('?') || resource.StartsWith('&'))
        {
            var baseHasQuery = !string.IsNullOrWhiteSpace(builder.Query);
            if (baseHasQuery && resource.StartsWith('?'))
                throw new FormatException($"Can't add resource '{resource}' to base URL '{baseUrl}' because the latter already has a query string.");
            if (!baseHasQuery && resource.StartsWith('&'))
                throw new FormatException($"Can't add resource '{resource}' to base URL '{baseUrl}' because the latter doesn't have a query string.");
            return new Uri(baseUrl + resource);
        }

        // make absolute
        if (!builder.Path.EndsWith('/'))
        {
            builder.Path += '/';
            baseUrl = builder.Uri;
        }

        return new Uri(baseUrl, resource);
    }

    private static IEnumerable<KeyValuePair<string, object?>> GetKeyValueArguments(object? arguments)
    {
        if (arguments == null) return [];

        return arguments.GetType().GetRuntimeProperties()
            .Where(t => t.CanRead && t.GetIndexParameters().Length == 0)
            .Select(t => new KeyValuePair<string, object?>(t.Name, t.GetValue(arguments)));
    }

    #endregion
}

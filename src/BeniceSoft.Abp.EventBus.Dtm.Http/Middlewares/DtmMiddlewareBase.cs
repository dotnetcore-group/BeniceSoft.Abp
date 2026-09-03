using BeniceSoft.Core;
using Microsoft.AspNetCore.Http;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public abstract class DtmMiddlewareBase
{
    protected virtual string GetHeaderValue(HttpContext context, string headerName)
    {
        if (context.Request.Headers.TryGetValue(headerName, out var headerValue))
        {
            return headerValue.ToStringSafe();
        }

        if (context.Request.Query.TryGetValue(headerName, out var queryValue))
        {
            return queryValue.ToStringSafe();
        }

        return string.Empty;
    }

    protected virtual async Task WriteResponseAsync(HttpContext context, int statusCode, string content)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync(content);
    }
}

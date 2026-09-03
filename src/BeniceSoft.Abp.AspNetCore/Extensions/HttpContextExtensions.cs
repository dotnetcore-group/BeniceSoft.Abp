using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text;

namespace BeniceSoft.Abp.AspNetCore.Extensions;

public static class HttpContextExtensions
{
    public static async Task<string?> ReadRequestBodyAsync(this HttpContext context,
        ILogger logger,
        bool enableBuffering = false)
    {
        if ((context.Request.ContentLength ?? 0) <= 0)
        {
            return null;
        }

        context.Request.EnableBuffering();
        var builder = new StringBuilder();

        try
        {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            var buffer = new char[1024];
            while (true)
            {
                var read = await reader.ReadAsync(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    break;
                }

                builder.Append(buffer, 0, read);
            }
        }
        catch (Exception ex) when (ex is IOException or BadHttpRequestException or OperationCanceledException)
        {
            logger.LogWarning(ex, "读取请求体失败，TraceId={TraceId}", context.TraceIdentifier);
        }
        finally
        {
            if (context.Request.Body.CanSeek)
            {
                context.Request.Body.Position = 0;
            }
        }

        return builder.Length > 0 ? builder.ToString() : null;
    }
}

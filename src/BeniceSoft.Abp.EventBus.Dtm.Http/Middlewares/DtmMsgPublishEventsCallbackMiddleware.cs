using BeniceSoft.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.EventBus.Distributed;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public class DtmMsgPublishEventsCallbackMiddleware : DtmMiddlewareBase
{
    private readonly RequestDelegate _next;

    public DtmMsgPublishEventsCallbackMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IOptions<DtmHttpOptions> dtmHttpOptions,
        IActionApiTokenChecker actionApiTokenChecker,
        IDistributedEventBus distributedEventBus,
        ILogger<DtmMsgPublishEventsCallbackMiddleware> logger)
    {
        var options = dtmHttpOptions.Value;
        var path = context.Request.Path.Value;

        if (!string.Equals(path, options.PublishEventsPath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var token = GetHeaderValue(context, DtmRequestHeaderNames.ActionApiToken);
        if (string.IsNullOrEmpty(token) || !await actionApiTokenChecker.IsCorrectAsync(token))
        {
            await WriteResponseAsync(context, 401, "Invalid ActionApiToken");
            return;
        }

        await HandlePublishEventsAsync(context, distributedEventBus, logger);
    }

    private async Task HandlePublishEventsAsync(
        HttpContext context,
        IDistributedEventBus distributedEventBus,
        ILogger<DtmMsgPublishEventsCallbackMiddleware> logger)
    {
        var gid = GetHeaderValue(context, "gid") ?? GetHeaderValue(context, "Gid");

        try
        {
            using var streamReader = new StreamReader(context.Request.Body);
            var body = await streamReader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new InvalidOperationException("PublishEvents 请求体为空。");
            }

            var payload = ParsePayload(body);
            var bytes = StringUtils.Hex36Bytes(payload);
            var eventInfos = JsonUtils.DeserializeBytes<List<OutgoingEventInfo>>(bytes) ?? [];

            await distributedEventBus
                .AsSupportsEventBoxes()
                .PublishManyFromOutboxAsync(eventInfos, new OutboxConfig("DTM_OutBox"));

            await WriteResponseAsync(context, 200, "SUCCESS");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DTM PublishEvents: 处理异常，TraceId={TraceId}, Gid={Gid}", context.TraceIdentifier, gid);
            await WriteResponseAsync(context, 500, $"FAILED: {ex.Message}");
        }
    }

    private static string ParsePayload(string body)
    {
        var trimmed = body.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            var request = JsonUtils.Deserialize<DtmMsgPublishEventsRequest>(trimmed);
            return request?.OutgoingEventInfoListToByteString ?? string.Empty;
        }

        return trimmed.Trim('"');
    }
}

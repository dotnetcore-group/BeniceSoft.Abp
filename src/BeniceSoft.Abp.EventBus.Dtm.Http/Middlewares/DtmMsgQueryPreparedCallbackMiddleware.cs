using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public class DtmMsgQueryPreparedCallbackMiddleware : DtmMiddlewareBase
{
    private readonly RequestDelegate _next;
    public DtmMsgQueryPreparedCallbackMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IOptions<DtmHttpOptions> dtmHttpOptions,
        IActionApiTokenChecker actionApiTokenChecker,
        IDtmQueryPreparedHandler dtmQueryPreparedHandler,
        ILogger<DtmMsgQueryPreparedCallbackMiddleware> logger)
    {
        var options = dtmHttpOptions.Value;
        var path = context.Request.Path.Value;

        if (!string.Equals(path, options.QueryPreparedPath, StringComparison.OrdinalIgnoreCase))
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

        await HandleQueryPreparedAsync(context, dtmQueryPreparedHandler, logger);
    }

    private async Task HandleQueryPreparedAsync(
        HttpContext context,
        IDtmQueryPreparedHandler dtmQueryPreparedHandler,
        ILogger<DtmMsgQueryPreparedCallbackMiddleware> logger)
    {
        var gid = GetHeaderValue(context, "gid") ?? GetHeaderValue(context, "Gid");

        try
        {
            var dbContextTypeName = GetHeaderValue(context, DtmRequestHeaderNames.DbContextType);
            var hashedConnectionString = GetHeaderValue(context, DtmRequestHeaderNames.HashedConnectionString);

            if (string.IsNullOrWhiteSpace(dbContextTypeName) ||
                string.IsNullOrWhiteSpace(hashedConnectionString) ||
                string.IsNullOrWhiteSpace(gid))
            {
                throw new AbpException("DTM QueryPrepared 缺少必要参数(dbContextTypeName/hashedConnectionString/gid)。");
            }

            if (!await dtmQueryPreparedHandler.CanHandleAsync(dbContextTypeName))
            {
                throw new AbpException($"DTM QueryPrepared 当前服务无法处理 DbContextType={dbContextTypeName}");
            }

            var result = await dtmQueryPreparedHandler.TryInsertBarrierAsRollbackAsync(
                dbContextTypeName,
                hashedConnectionString,
                gid);

            if (!result)
            {
                throw new AbpException($"DTM QueryPrepared 插入回滚屏障失败，Gid={gid}");
            }

            await WriteResponseAsync(context, 200, "SUCCESS");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DTM QueryPrepared: 处理异常，TraceId={TraceId}, Gid={Gid}", context.TraceIdentifier, gid);
            await WriteResponseAsync(context, 500, $"FAILED: {ex.Message}");
        }
    }
}

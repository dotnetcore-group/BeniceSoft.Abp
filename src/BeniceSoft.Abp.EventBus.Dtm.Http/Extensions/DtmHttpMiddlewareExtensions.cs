using Microsoft.AspNetCore.Builder;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public static class DtmHttpMiddlewareExtensions
{
    /// <summary>
    /// DTM HTTP 中间件
    /// 注意：此中间件应放在认证中间件之前，以绕过标准认证流程
    /// </summary>
    /// <example>
    /// app.UseDtmHttpMiddleware();
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// </example>
    public static IApplicationBuilder UseDtmHttpMiddleware(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<DtmMsgQueryPreparedCallbackMiddleware>();
        builder.UseMiddleware<DtmMsgPublishEventsCallbackMiddleware>();
        builder.UseMiddleware<DtmTccCallbackMiddleware>();
        builder.UseMiddleware<DtmSagaCallbackMiddleware>();

        return builder;
    }
}
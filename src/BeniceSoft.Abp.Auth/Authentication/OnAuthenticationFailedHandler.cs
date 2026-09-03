using System.Net;
using BeniceSoft.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Auth.Authentication;

/// <summary>
/// JWT 认证失败后处理者
/// </summary>
public class OnAuthenticationFailedHandler : ITransientDependency
{
    private readonly ILogger<OnAuthenticationFailedHandler> _logger;

    public OnAuthenticationFailedHandler(ILogger<OnAuthenticationFailedHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(AuthenticationFailedContext authenticationFailedContext)
    {
        var context = authenticationFailedContext.HttpContext;
        var exception = authenticationFailedContext.Exception;

        var authHeader = context.Request.Headers["Authorization"].ToStringSafe();

        _logger.LogWarning(exception,
            "JWT authentication failed. Authorization header length: {Length}, Header value (first 100 chars): {HeaderPreview}, Error: {Message}",
            authHeader.Length,
            authHeader.Length > 100 ? authHeader[..100] + "..." : authHeader,
            exception.Message);

        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        await context.Response.WriteAsJsonAsync(
            new ResponseResult(HttpStatusCode.Unauthorized,
            NoAuthorizationException.DefaultMessage));

        await context.Response.CompleteAsync();
    }
}
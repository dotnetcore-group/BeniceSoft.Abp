using BeniceSoft.Abp.Auth.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpenIddict.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;

namespace BeniceSoft.Abp.Auth;

/// <summary>
/// JWT 认证成功后处理者
/// </summary>
public class OnTokenValidatedHandler : ITransientDependency
{
    private readonly IUserSessionStore _userSessionStore;

    public OnTokenValidatedHandler(IUserSessionStore userSessionStore)
    {
        _userSessionStore = userSessionStore;
    }

    public async Task HandleAsync(TokenValidatedContext context)
    {
        var userId = context.Principal?.FindFirst(OpenIddictConstants.Claims.Subject)?.GetLongValue();
        if (userId is null)
        {
            context.Fail("Invalid subject.");
            return;
        }

        var clientId = context.Principal?.FindFirst(AbpClaimTypes.ClientId)?.Value;
        if (string.IsNullOrEmpty(clientId))
        {
            context.Fail("Invalid client_id.");
            return;
        }

        var issuedAt = context.Principal?.FindFirst(OpenIddictConstants.Claims.IssuedAt)?.Value;
        if (string.IsNullOrEmpty(issuedAt))
        {
            context.Fail("Invalid iat.");
            return;
        }

        var result = await _userSessionStore.VerifyExpirAsync(userId.Value, clientId, issuedAt);
        if (!result)
        {
            context.Fail("Session revoked.");
            return;
        }
    }
}
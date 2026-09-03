using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace BeniceSoft.Abp.Auth.Authorization;

/// <summary>
/// 授权处理
/// </summary>
public class BeniceSoftAuthorizationHandler : AuthorizationHandler<BeniceSoftAuthorizationRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BeniceSoftAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, BeniceSoftAuthorizationRequirement requirement)
    {
        if (context.PendingRequirements.All(r => !(r is BeniceSoftAuthorizationRequirement)))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // 检查用户是否已认证
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null || user.Identity is null || !user.Identity.IsAuthenticated)
        {
            context.Fail();
            return Task.CompletedTask;
        }

        context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
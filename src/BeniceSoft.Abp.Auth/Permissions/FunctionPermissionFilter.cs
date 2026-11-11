using BeniceSoft.Abp.Auth.Core;
using BeniceSoft.Abp.Core.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BeniceSoft.Abp.Auth.Permissions;

public class FunctionPermissionFilter : IAuthorizationFilter
{
    private readonly string[] _permissionCodes;
    private readonly ICurrentUserPermissionAccessor _accessor;
    private readonly IBeniceSoftCurrentUser _currentUser;

    public FunctionPermissionFilter(
        string[] permissionCodes,
        ICurrentUserPermissionAccessor accessor,
        IBeniceSoftCurrentUser currentUser)
    {
        _permissionCodes = permissionCodes;
        _accessor = accessor;
        _currentUser = currentUser;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // 已认证的内置管理员直接通过功能权限码检查
        if (context.HttpContext.User.Identity?.IsAuthenticated == true
            && _currentUser.IsAuthenticated
            && _currentUser.FindClaim(BeniceSoftAuthConstants.ClaimTypes.TokenType)?.Value
                != BeniceSoftAuthConstants.TokenTypes.Machine
            && string.Equals(_currentUser.UserName, "ADMIN", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var permissions = _accessor.UserPermission?.FunctionPermissions;
        if (!(permissions?.Intersect(_permissionCodes).Any() ?? false))
        {
            context.Result = new ForbidResult();
        }
    }
}

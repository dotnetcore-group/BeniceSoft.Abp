using BeniceSoft.Abp.Auth.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BeniceSoft.Abp.Auth.Permissions;

public class FunctionPermissionFilter : IAuthorizationFilter
{
    private readonly string[] _permissionCodes;
    private readonly ICurrentUserPermissionAccessor _accessor;

    public FunctionPermissionFilter(string[] permissionCodes, ICurrentUserPermissionAccessor accessor)
    {
        _permissionCodes = permissionCodes;
        _accessor = accessor;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var permissions = _accessor.UserPermission?.FunctionPermissions;
        if (!(permissions?.Intersect(_permissionCodes).Any() ?? false))
        {
            context.Result = new ForbidResult();
        }
    }
}

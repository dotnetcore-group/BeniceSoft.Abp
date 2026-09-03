using Microsoft.AspNetCore.Mvc;

namespace BeniceSoft.Abp.Auth.Permissions;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class FunctionPermissionAttribute : TypeFilterAttribute
{
    public FunctionPermissionAttribute(params string[] permissionCodes) : base(typeof(FunctionPermissionFilter))
    {
        Arguments = [permissionCodes];
    }
}

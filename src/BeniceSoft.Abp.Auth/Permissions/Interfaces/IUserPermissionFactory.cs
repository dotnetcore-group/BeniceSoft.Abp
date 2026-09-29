using BeniceSoft.Abp.Auth.Core;
using Microsoft.AspNetCore.Http;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Auth.Permissions;

public interface IUserPermissionFactory : ISingletonDependency
{
    /// <summary>
    /// 组装用户权限
    /// </summary>
    Task<IUserPermission> CreateAsync(long userId, HttpContext httpContext);

    /// <summary>
    /// 后台Job组装用户权限（无 HttpContext）
    /// </summary>
    Task<IUserPermission> CreateForBackgroundAsync(long userId);
}
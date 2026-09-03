using BeniceSoft.Abp.Auth.Core;
using Microsoft.AspNetCore.Http;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Auth.Permissions;

public interface IUserPermissionFactory : ISingletonDependency
{
    Task<IUserPermission> CreateAsync(long userId, HttpContext httpContext);
}
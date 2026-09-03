using BeniceSoft.Abp.Auth.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore.Interceptors;

/// <summary>
/// 字段权限拦截器扩展方法
/// </summary>
public static class FieldPermissionInterceptorExtensions
{
    /// <summary>
    /// 添加字段权限拦截器
    /// </summary>
    /// <param name="optionsBuilder">DbContext 选项构建器</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns></returns>
    public static DbContextOptionsBuilder AddFieldPermissionInterceptor(
        this DbContextOptionsBuilder optionsBuilder,
        IServiceProvider serviceProvider)
    {
        var permissionAccessor = serviceProvider.GetRequiredService<ICurrentUserPermissionAccessor>();
        var interceptor = new FieldPermissionSaveChangesInterceptor(permissionAccessor);
        optionsBuilder.AddInterceptors(interceptor);
        return optionsBuilder;
    }

    /// <summary>
    /// 添加字段权限拦截器
    /// </summary>
    /// <param name="optionsBuilder">DbContext 选项构建器</param>
    /// <param name="permissionAccessor">权限访问器</param>
    /// <returns></returns>
    public static DbContextOptionsBuilder AddFieldPermissionInterceptor(
        this DbContextOptionsBuilder optionsBuilder,
        ICurrentUserPermissionAccessor permissionAccessor)
    {
        var interceptor = new FieldPermissionSaveChangesInterceptor(permissionAccessor);
        optionsBuilder.AddInterceptors(interceptor);
        return optionsBuilder;
    }
}

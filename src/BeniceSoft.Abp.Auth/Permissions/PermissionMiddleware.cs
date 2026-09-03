using BeniceSoft.Abp.Auth.Core;
using BeniceSoft.Abp.Core.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Auth.Permissions;

public class PermissionMiddleware : IMiddleware, ITransientDependency
{
    private readonly IUserPermissionFactory _userPermissionFactory;
    private readonly ICurrentUserPermissionAccessor _currentUserPermissionAccessor;
    private readonly ILogger<PermissionMiddleware> _logger;
    private readonly IBeniceSoftCurrentUser _currentUser;

    public PermissionMiddleware(
        IUserPermissionFactory userPermissionFactory,
        ICurrentUserPermissionAccessor currentUserPermissionAccessor,
        ILogger<PermissionMiddleware> logger,
        IBeniceSoftCurrentUser currentUser)
    {
        _userPermissionFactory = userPermissionFactory;
        _currentUserPermissionAccessor = currentUserPermissionAccessor;
        _logger = logger;
        _currentUser = currentUser;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            // 跳过 OAuth 授权端点（/connect/authorize, /connect/token 等）
            if (!context.Request.Path.StartsWithSegments("/connect") && _currentUser.IsAuthenticated)
            {
                var userId = _currentUser.Id!.Value;

                _logger.LogInformation("Initialize user {0} permissions", userId);

                var userPermission = await _userPermissionFactory.CreateAsync(userId, context);

                context.Features.Set(userPermission);
            }

            await next(context);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, e.Message);
            throw;
        }
        finally
        {
            _currentUserPermissionAccessor.UserPermission = null;
        }
    }
}
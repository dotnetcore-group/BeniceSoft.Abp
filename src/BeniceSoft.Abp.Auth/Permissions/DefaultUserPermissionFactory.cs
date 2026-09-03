using BeniceSoft.Abp.Auth.Core;
using BeniceSoft.Abp.Auth.Core.Models;
using BeniceSoft.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using static BeniceSoft.Abp.Auth.Core.BeniceSoftAuthConstants;

namespace BeniceSoft.Abp.Auth.Permissions;

/// <summary>
/// 用户权限工厂：按 Row / Field / Function 三个维度分片缓存，请求时组装为 UserPermission。
/// </summary>
public class DefaultUserPermissionFactory : IUserPermissionFactory
{
    private readonly ICurrentUserPermissionAccessor _userPermissionAccessor;
    private readonly IPermissionCenterClient _permissionCenterClient;
    private readonly ILogger<DefaultUserPermissionFactory> _logger;
    private readonly IDistributedCache _distributedCache;

    public DefaultUserPermissionFactory(
        ICurrentUserPermissionAccessor userPermissionAccessor,
        IPermissionCenterClient permissionCenterClient,
        ILogger<DefaultUserPermissionFactory> logger,
        IDistributedCache distributedCache)
    {
        _userPermissionAccessor = userPermissionAccessor;
        _permissionCenterClient = permissionCenterClient;
        _logger = logger;
        _distributedCache = distributedCache;
    }

    public async Task<IUserPermission> CreateAsync(long userId, HttpContext httpContext)
    {
        var accessToken = httpContext.Request.Headers[HeaderNames.Authorization].ToString();

        var rowPermissions = await GetOrLoadRowPermissionsAsync(userId, accessToken);
        var fieldPermissions = await GetOrLoadFieldPermissionsAsync(userId, accessToken);
        var functionPermissions = await GetOrLoadFunctionPermissionsAsync(userId, accessToken);

        var userPermission = new UserPermission
        {
            IsInitialized = true,
            UserId = userId,
            RowPermissions = rowPermissions,
            FieldPermissions = fieldPermissions,
            FunctionPermissions = functionPermissions,
        };

        return Initialize(userPermission);
    }

    private IUserPermission Initialize(IUserPermission userPermission)
    {
        _userPermissionAccessor.UserPermission = userPermission;
        _logger.LogInformation("Initialized user {0} permissions.", userPermission.UserId);
        return userPermission;
    }

    private async Task<List<RowPermission>?> GetOrLoadRowPermissionsAsync(long userId, string accessToken)
    {
        var cached = await GetFromCacheAsync<List<RowPermission>>(Cache.GetUserRowPermissionKey(userId));
        if (cached is not null)
        {
            return cached;
        }

        var loaded = await _permissionCenterClient.GetUserRowPermissions(userId, accessToken);
        await SetToCacheAsync(Cache.GetUserRowPermissionKey(userId), loaded);
        return loaded;
    }

    private async Task<List<FieldPermission>?> GetOrLoadFieldPermissionsAsync(long userId, string accessToken)
    {
        var cached = await GetFromCacheAsync<List<FieldPermission>>(Cache.GetUserFieldPermissionKey(userId));
        if (cached is not null)
        {
            return cached;
        }

        var loaded = await _permissionCenterClient.GetUserFieldPermissions(userId, accessToken);
        await SetToCacheAsync(Cache.GetUserFieldPermissionKey(userId), loaded);
        return loaded;
    }

    private async Task<List<string>?> GetOrLoadFunctionPermissionsAsync(long userId, string accessToken)
    {
        var cached = await GetFromCacheAsync<List<string>>(Cache.GetUserFunctionPermissionKey(userId));
        if (cached is not null)
        {
            return cached;
        }

        var loaded = await _permissionCenterClient.GetUserFunctionPermissions(userId, accessToken);
        await SetToCacheAsync(Cache.GetUserFunctionPermissionKey(userId), loaded);
        return loaded;
    }

    private async Task<T?> GetFromCacheAsync<T>(string key)
    {
        var bytes = await _distributedCache.GetAsync(key);
        if (bytes is null)
        {
            return default;
        }

        return JsonUtils.DeserializeBytes<T>(bytes);
    }

    private async Task SetToCacheAsync<T>(string key, T? value)
    {
        if (value is null)
        {
            return;
        }

        var bytes = JsonUtils.SerializeBytes(value);
        await _distributedCache.SetAsync(key, bytes);
    }
}

using BeniceSoft.Abp.Auth.Core;
using Microsoft.Extensions.Caching.Distributed;
using static BeniceSoft.Abp.Auth.Core.BeniceSoftAuthConstants;

namespace BeniceSoft.Abp.Auth;

public class UserSessionStore : IUserSessionStore
{
    private readonly IDistributedCache _distributedCache;

    public UserSessionStore(IDistributedCache distributedCache)
    {
        _distributedCache = distributedCache;
    }

    public async Task StoreAsync(long userId, string clientId, string issuedAt)
    {
        var key = Cache.GetUserSessionKey(userId, clientId);
        await _distributedCache.SetStringAsync(key, issuedAt, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(Cache.AccessTokenLifetime)
        });
    }

    public async Task BumpAsync(long userId, string clientId)
    {
        var key = Cache.GetUserSessionKey(userId, clientId);
        await _distributedCache.RemoveAsync(key);
    }

    public async Task<bool> VerifyExpirAsync(long userId, string clientId, string issuedAt)
    {
        var key = Cache.GetUserSessionKey(userId, clientId);
        var issuedAtCacheValue = await _distributedCache.GetStringAsync(key);
        if (!string.IsNullOrEmpty(issuedAtCacheValue) && issuedAtCacheValue == issuedAt)
        {
            return true;
        }

        return false;
    }
}

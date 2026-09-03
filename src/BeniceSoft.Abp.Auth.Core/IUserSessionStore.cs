using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Auth.Core;

/// <summary>
/// 用户状态存储对象(用户登录成功后，标记用户是否处于登录状态)
/// </summary>
public interface IUserSessionStore : ISingletonDependency
{
    Task StoreAsync(long userId, string clientId, string issuedAt);

    Task BumpAsync(long userId, string clientId);

    Task<bool> VerifyExpirAsync(long userId, string clientId, string issuedAt);
}

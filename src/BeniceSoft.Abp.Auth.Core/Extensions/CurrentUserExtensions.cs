using BeniceSoft.Abp.Core.Users;

namespace BeniceSoft.Abp.Auth.Core;

public static class CurrentUserExtensions
{
    /// <summary>
    /// 获取当前用户角色id集合
    /// </summary>
    /// <param name="currentUser"></param>
    /// <returns></returns>
    public static List<long>? GetRoleIds(this IBeniceSoftCurrentUser currentUser)
    {
        var claims = currentUser.FindClaims(BeniceSoftAuthConstants.ClaimTypes.RoleId);
        return claims?.Select(x => x.GetLongValue())
            .Where(x => x.HasValue)
            .Select(x => x!.Value).ToList();
    }
}
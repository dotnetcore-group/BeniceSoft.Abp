using System.Security.Claims;

namespace BeniceSoft.Abp.Core.Users;

/// <summary>
/// 当前用户接口
/// </summary>
public interface IBeniceSoftCurrentUser
{
    /// <summary>
    /// 是否已认证
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// 用户ID
    /// </summary>
    long? Id { get; }

    /// <summary>
    /// 租户ID
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// 客户端应用Id
    /// </summary>
    string ClientId { get; }

    /// <summary>
    /// 用户名
    /// </summary>
    string UserName { get; }

    /// <summary>
    /// 昵称
    /// </summary>
    string NickName { get; }

    /// <summary>
    /// 姓名
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 姓
    /// </summary>
    string SurName { get; }

    /// <summary>
    /// 主部门
    /// </summary>
    string DepartmentName { get; }

    /// <summary>
    /// 手机号
    /// </summary>
    string PhoneNumber { get; }

    /// <summary>
    /// 手机号是否验证
    /// </summary>
    bool PhoneNumberVerified { get; }

    /// <summary>
    /// 邮箱
    /// </summary>
    string Email { get; }

    /// <summary>
    /// 邮箱是否验证
    /// </summary>
    bool EmailVerified { get; }

    /// <summary>
    /// 角色名称列表
    /// </summary>
    string[] Roles { get; }

    /// <summary>
    /// 角色ID列表
    /// </summary>
    long[] RoleIds { get; }

    /// <summary>
    /// 查找指定类型的 Claim
    /// </summary>
    Claim? FindClaim(string claimType);

    /// <summary>
    /// 查找指定类型的所有 Claims
    /// </summary>
    Claim[] FindClaims(string claimType);

    /// <summary>
    /// 获取所有 Claims
    /// </summary>
    Claim[] GetAllClaims();

    /// <summary>
    /// 是否属于指定角色
    /// </summary>
    bool IsInRole(string roleName);

}


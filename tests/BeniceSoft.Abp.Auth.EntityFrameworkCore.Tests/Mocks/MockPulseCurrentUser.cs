using System.Security.Claims;
using BeniceSoft.Abp.Core.Users;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests.Mocks;

/// <summary>
/// 模拟的 BeniceSoft 当前用户
/// </summary>
public class MockBeniceSoftCurrentUser : IBeniceSoftCurrentUser
{
    public bool IsAuthenticated { get; set; }
    public long? Id { get; set; }
    public Guid? TenantId { get; set; }
    public string ClientId { get; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string NickName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SurName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool PhoneNumberVerified { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string[] Roles { get; set; } = [];
    public long[] RoleIds { get; set; } = [];

    public Claim? FindClaim(string claimType) => null;
    public Claim[] FindClaims(string claimType) => [];
    public Claim[] GetAllClaims() => [];
    public bool IsInRole(string roleName) => Roles.Contains(roleName);
}


using BeniceSoft.Abp.Auth.Core;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests.Mocks;

/// <summary>
/// 模拟的用户权限访问器
/// </summary>
public class MockCurrentUserPermissionAccessor : ICurrentUserPermissionAccessor
{
    public IUserPermission? UserPermission { get; set; }
}


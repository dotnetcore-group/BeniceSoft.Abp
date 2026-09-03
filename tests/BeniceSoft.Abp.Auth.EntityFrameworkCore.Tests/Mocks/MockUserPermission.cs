using BeniceSoft.Abp.Auth.Core;
using BeniceSoft.Abp.Auth.Core.Models;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests.Mocks;

/// <summary>
/// 模拟的用户权限
/// </summary>
public class MockUserPermission : IUserPermission
{
    public bool IsInitialized { get; set; } = true;

    public long UserId { get; set; }

    public List<RowPermission>? RowPermissions { get; set; }

    public List<FieldPermission>? FieldPermissions { get; set; }
    public List<string>? FunctionPermissions { get; set; }
}


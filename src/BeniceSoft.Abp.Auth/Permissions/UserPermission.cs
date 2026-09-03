using BeniceSoft.Abp.Auth.Core;
using BeniceSoft.Abp.Auth.Core.Models;

namespace BeniceSoft.Abp.Auth.Permissions;

[Serializable]
public class UserPermission : IUserPermission
{
    public bool IsInitialized { get; set; }
    public long UserId { get; set; }
    public List<RowPermission>? RowPermissions { get; set; }
    public List<FieldPermission>? FieldPermissions { get; set; }
    public List<string>? FunctionPermissions { get; set; }
}
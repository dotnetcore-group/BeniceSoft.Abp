using BeniceSoft.Abp.Auth.Core.Models;

namespace BeniceSoft.Abp.Auth.Permissions;

public interface IPermissionCenterClient
{
    /// <summary>
    /// 获取用户行权限
    /// </summary>
    Task<List<RowPermission>?> GetUserRowPermissions(long userId, string accessToken);

    /// <summary>
    /// 获取用户字段权限
    /// </summary>
    Task<List<FieldPermission>?> GetUserFieldPermissions(long userId, string accessToken);

    /// <summary>
    /// 获取用户方法/API功能授权码
    /// </summary>
    Task<List<string>?> GetUserFunctionPermissions(long userId, string accessToken);
}

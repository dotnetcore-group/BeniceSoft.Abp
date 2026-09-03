using BeniceSoft.Abp.Auth.Core.Models;

namespace BeniceSoft.Abp.Auth.Core;

/// <summary>
/// 当前用户权限
/// </summary>
public interface IUserPermission
{
    /// <summary>
    /// 是否已初始化
    /// </summary>
    public bool IsInitialized { get; }

    /// <summary>
    /// 用户id
    /// </summary>
    public long UserId { get; }

    /// <summary>
    /// 行权限
    /// </summary>
    public List<RowPermission>? RowPermissions { get; }

    /// <summary>
    /// 字段权限
    /// </summary>
    public List<FieldPermission>? FieldPermissions { get; }

    /// <summary>
    /// 方法权限
    /// </summary>
    public List<string>? FunctionPermissions { get; set; }
}
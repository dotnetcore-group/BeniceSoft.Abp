using System.ComponentModel;

namespace BeniceSoft.Abp.Auth.Core.Models;

public class FieldPermission
{
    /// <summary>
    /// 表名
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// 字段名
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// 字段权限等级 
    /// 1：隐藏 
    /// 2：只读
    /// 4：读写
    /// 根据【FieldAuthLevelEnum】进行位运算的结果
    /// 一个角色可以组合权限，
    /// 比如同时拥有只读和读写，那么这个值就等于6，无权限和只读那么这个值等于3
    /// 这样只需要直接比较结果值就能知道当前角色的字段权限
    /// </summary>
    public int FieldAuthLevel { get; set; }

    /// <summary>
    /// 当前字段是否显示
    /// </summary>
    public bool IsDisplay { get; set; }
}

public enum FieldAuthLevelEnum
{
    /// <summary>
    /// 隐藏 
    /// </summary>
    [Description("无权限")]
    None = 1,

    /// <summary>
    /// 只读
    /// </summary>
    [Description("只读")]
    ReadOnly = 2,

    /// <summary>
    /// 读写
    /// </summary>
    [Description("读写")]
    ReadWrite = 4,
}

namespace BeniceSoft.Abp.Auth.Core.Models;

public class RowPermissionCondition
{
    /// <summary>
    /// 是否数据超管
    /// </summary>
    public bool IsDataSuperAdmin { get; set; }

    /// <summary>
    /// 条件与条件之间的逻辑操作符
    /// and or
    /// </summary>
    public string LogicalOperator { get; set; } = string.Empty;

    /// <summary>
    /// 列名
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>
    /// 列名与值之间的操作符
    /// </summary>
    public string Operator { get; set; } = string.Empty;

    /// <summary>
    /// 值
    /// </summary>
    public List<string> Values { get; set; } = [];
}
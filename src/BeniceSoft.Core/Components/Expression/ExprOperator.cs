using System.Runtime.Serialization;

namespace BeniceSoft.Core;

public enum ExprOperator : int
{
    /// <summary>
    /// 无操作
    /// </summary>
    [EnumMember(Value = "none")]
    None = 0,

    /// <summary>
    /// 等于
    /// </summary>
    [EnumMember(Value = "equal")]
    Equal = 1,

    /// <summary>
    /// 不等于
    /// </summary>
    [EnumMember(Value = "not_equal")]
    NotEqual = 2,

    /// <summary>
    /// 大于
    /// </summary>
    [EnumMember(Value = "greater_than")]
    GreaterThan = 3,

    /// <summary>
    /// 大于等于
    /// </summary>
    [EnumMember(Value = "greater_than_or_equal")]
    GreaterThanOrEqual = 4,

    /// <summary>
    /// 小于
    /// </summary>
    [EnumMember(Value = "less_than")]
    LessThan = 5,

    /// <summary>
    /// 小于等于
    /// </summary>
    [EnumMember(Value = "less_than_or_equal")]
    LessThanOrEqual = 6,

    /// <summary>
    /// 包含（字符串模糊匹配）
    /// </summary>
    [EnumMember(Value = "contains")]
    Contains = 7,

    /// <summary>
    /// 不包含（字符串模糊匹配取反）
    /// </summary>
    [EnumMember(Value = "not_contains")]
    NotContains = 8,

    /// <summary>
    /// 开头匹配
    /// </summary>
    [EnumMember(Value = "starts_with")]
    StartsWith = 9,

    /// <summary>
    /// 结尾匹配
    /// </summary>
    [EnumMember(Value = "ends_with")]
    EndsWith = 10,

    /// <summary>
    /// 介于（范围查询）
    /// </summary>
    [EnumMember(Value = "between")]
    Between = 11,

    /// <summary>
    /// 在（集合包含）
    /// </summary>
    [EnumMember(Value = "in")]
    In = 12,

    /// <summary>
    /// 不在（集合不包含）
    /// </summary>
    [EnumMember(Value = "not_in")]
    NotIn = 13
}

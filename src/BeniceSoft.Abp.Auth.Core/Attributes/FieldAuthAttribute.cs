namespace BeniceSoft.Abp.Auth.Core;

/// <summary>
/// 字段权限过滤标签
/// 参数格式："表名.字段名"
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class FieldAuthAttribute : Attribute
{
    /// <summary>
    /// 表名.字段名
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// 隐藏后显示的值
    /// </summary>
    public string MaskedValue { get; private set; }

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="desc">表名.字段名</param>
    /// <param name="maskedValue">隐藏后显示的值</param>
    public FieldAuthAttribute(string desc, string maskedValue = "0")
    {
        Description = desc;
        MaskedValue = maskedValue;
    }
}

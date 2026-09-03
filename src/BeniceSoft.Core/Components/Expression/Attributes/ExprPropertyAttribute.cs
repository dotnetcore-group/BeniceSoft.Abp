namespace BeniceSoft.Core;

[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public class ExprPropertyAttribute(ExprOperator eop = ExprOperator.Equal) : Attribute
{

    /// <summary>
    /// 操作符
    /// </summary>
    public ExprOperator Operator { get; set; } = eop;

    /// <summary>
    /// 字段名称
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// 忽略的值
    /// </summary>
    public object IgnoreValue { get; set; } = string.Empty;
}
using BeniceSoft.Core;
using BeniceSoft.Core.Constants;

namespace BeniceSoft.Extensions.DynamicQuery;

public class DynamicQueryCondition
{
    /// <summary>
    /// 关系：and，or
    /// </summary>
    public string Relation { get; set; } = BeniceSoftRelationConstant.And;

    /// <summary>
    /// 字段名
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// 字段类型，支持： "integer", "double", "string", "date", "datetime", and "boolean".
    /// </summary>
    public string FieldType { get; set; } = string.Empty;

    /// <summary>
    /// 操作符
    /// </summary>
    public ExprOperator Operator { get; set; } = ExprOperator.Equal;

    /// <summary>
    /// 比较值
    /// </summary>
    public List<string> Value { get; set; } = new();
}
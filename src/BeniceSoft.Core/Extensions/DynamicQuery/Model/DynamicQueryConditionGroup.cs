using BeniceSoft.Core.Constants;

namespace BeniceSoft.Extensions.DynamicQuery;

public class DynamicQueryConditionGroup
{
    /// <summary>
    /// 关系：and，or
    /// </summary>
    public string Relation { get; set; } = BeniceSoftRelationConstant.And;

    /// <summary>
    /// 条件
    /// </summary>
    public List<DynamicQueryCondition> Conditions { get; set; } = new();
}
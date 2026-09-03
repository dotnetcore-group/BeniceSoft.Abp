namespace BeniceSoft.Extensions.DynamicQuery;

public interface IDynamicQueryRequest
{
    List<DynamicQueryConditionGroup>? ConditionGroups { get; set; }
}
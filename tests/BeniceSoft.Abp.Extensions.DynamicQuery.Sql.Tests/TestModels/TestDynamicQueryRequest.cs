using BeniceSoft.Extensions.DynamicQuery;

namespace BeniceSoft.Abp.Extensions.DynamicQuery.Sql.Tests.TestModels;

/// <summary>
/// 测试用的 DynamicQueryRequest 实现
/// </summary>
public class TestDynamicQueryRequest : IDynamicQueryRequest
{
    public List<DynamicQueryConditionGroup>? ConditionGroups { get; set; }
}


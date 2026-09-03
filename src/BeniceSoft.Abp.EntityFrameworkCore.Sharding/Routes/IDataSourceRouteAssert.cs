namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 路由断言
/// </summary>
public interface IDataSourceRouteAssert
{
    /// <summary>
    /// 断言路由结果
    /// </summary>
    /// <param name="allDataSource">所有的路由数据源</param>
    /// <param name="result">本次查询路由返回结果</param>
    void Assert(IReadOnlyList<string> allDataSource, IReadOnlyList<string> result);
}

public interface IDataSourceRouteAssert<T> : IDataSourceRouteAssert
    where T : class
{
}

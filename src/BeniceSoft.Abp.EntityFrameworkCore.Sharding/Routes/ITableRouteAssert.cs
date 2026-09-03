namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 路由断言
/// </summary>
public interface ITableRouteAssert
{
    void Assert(DataSourceRouteResult result, IReadOnlyList<string> tails, IReadOnlyList<TableRouteUnit> units);
}

public interface ITableRouteAssert<T> : ITableRouteAssert
    where T : class
{
}

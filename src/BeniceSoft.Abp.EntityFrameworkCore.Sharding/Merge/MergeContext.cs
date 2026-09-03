namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public class MergeContext(IEnumerable<TableRouteResult> results)
{
    public IEnumerable<TableRouteResult> Results { get; } = results;
}

/// <summary>
/// 构造函数
/// </summary>
/// <param name="accessor"></param>
/// <param name="previous"></param>
public class MergeScope(IMergeAccessor accessor, MergeContext? previous) : IDisposable
{
    public IMergeAccessor Accessor { get; } = accessor;

    public void Dispose()
    {
        Accessor.Context = previous;
        GC.SuppressFinalize(this);
    }
}

internal sealed class MergerSqlUnit(ConnectionMode connectionMode, ISqlRouteUnit routeUnit)
{
    public ISqlRouteUnit RouteUnit { get; } = routeUnit;

    public ConnectionMode ConnectionMode { get; } = connectionMode;
}

internal sealed class MergerSqlGroup<T>(ConnectionMode connectionMode, IReadOnlyList<T> groups)
{
    public ConnectionMode ConnectionMode { get; } = connectionMode;

    /// <summary>
    /// 执行组
    /// </summary>
    public IReadOnlyList<T> Groups { get; } = groups;
}

internal sealed class DataSourceMergerSqlUnit(ConnectionMode connectionMode, IReadOnlyList<MergerSqlGroup<MergerSqlUnit>> groups)
{
    public ConnectionMode ConnectionMode { get; } = connectionMode;

    /// <summary>
    /// 执行组
    /// </summary>
    public IReadOnlyList<MergerSqlGroup<MergerSqlUnit>> Groups { get; } = groups;
}

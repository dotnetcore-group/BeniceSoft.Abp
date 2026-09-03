namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IMergeManager
{
    MergeContext? Current { get; }

    /// <summary>
    /// 创建scope
    /// </summary>
    /// <returns></returns>
    MergeScope CreateScope(IEnumerable<TableRouteResult> tableRouteResults);
}

internal sealed class MergeManager(IMergeAccessor accessor) : IMergeManager
{
    public MergeContext? Current => accessor.Context;

    public MergeScope CreateScope(IEnumerable<TableRouteResult> tableRouteResults)
    {
        var previous = accessor.Context;
        accessor.Context = new MergeContext(tableRouteResults);
        return new MergeScope(accessor, previous);
    }
}

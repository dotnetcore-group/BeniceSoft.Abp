using BeniceSoft.Core;
namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal sealed class SequencePagedList(IEnumerable<RouteQueryResult<long>> results)
{
    private int? _skip;
    private int? _take;

    public SequencePagedList WithSkip(int? skip)
    {
        _skip = skip;
        return this;
    }

    public SequencePagedList WithTake(int? take)
    {
        _take = take;
        return this;
    }

    public ICollection<SequenceResult> ToList()
    {
        ICollection<SequenceResult> routeResults = new LinkedList<SequenceResult>();

        var currentSkip = _skip.GetValueOrDefault();
        var currentTake = _take;
        var stopSkip = false;
        var needBreak = false;

        foreach (var routeQueryResult in results)
        {
            if (!stopSkip)
            {
                if (routeQueryResult.Result > currentSkip)
                {
                    stopSkip = true;
                }
                else
                {
                    currentSkip -= routeQueryResult.Result.ToInt32();
                    continue;
                }
            }

            var currentRealSkip = currentSkip;
            var currentRealTake = routeQueryResult.Result.ToInt32() - currentSkip;
            if (currentSkip != 0L)
            {
                currentSkip = 0;
            }

            if (currentTake.HasValue)
            {
                if (currentTake.Value <= currentRealTake)
                {
                    currentRealTake = currentTake.Value;
                    needBreak = true;
                }
                else
                {
                    currentTake = currentTake.Value - currentRealTake;
                }
            }

            var sequenceResult = new SequenceResult(currentRealSkip, currentRealTake, routeQueryResult);
            routeResults.Add(sequenceResult);

            if (needBreak)
            {
                break;
            }
        }

        return routeResults;
    }
}

internal sealed class SequenceResult(int skip, int take, RouteQueryResult<long> routeQueryResult)
{
    public int Skip { get; } = skip;

    public int Take { get; } = take;

    public string DataSource { get; } = routeQueryResult.DataSource!;

    public TableRouteResult RouteResult { get; } = routeQueryResult.TableRouteResult!;
}

public class SequenceQueryMatch(bool sameTailComparer, SequenceMatchMode mode)
{
    public bool SameTailComparer { get; } = sameTailComparer;

    public SequenceMatchMode Mode { get; } = mode;
}

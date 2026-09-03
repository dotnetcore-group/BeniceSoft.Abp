using BeniceSoft.Core;
using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IShardingMerger<T>
{
    T StreamMerge(List<T> parallelResults);

    void MemoryMerge(List<T> memoryResults, List<T> parallelResults);
}

internal class EnumerableShardingMerger<T>(StreamMergeContext context, bool async) : IShardingMerger<IStreamMergeEnumerator<T>>
{
    protected StreamMergeContext Context { get; } = context;

    public void MemoryMerge(List<IStreamMergeEnumerator<T>> memoryResults, List<IStreamMergeEnumerator<T>> parallelResults)
    {
        var count = memoryResults.Count;
        if (count > 1)
        {
            throw new ShardingInvalidOperationException(
                $"{typeof(T)} {nameof(memoryResults)} has more than one element in container");
        }

        var parallelCount = parallelResults.Count;
        if (parallelCount == 0)
        {
            return;
        }

        //聚合
        if (parallelResults is IEnumerable<IStreamMergeEnumerator<T>> result)
        {
            var mergeAsyncEnumerators = new List<IStreamMergeEnumerator<T>>(parallelResults.Count + count);
            if (count == 1)
            {
                mergeAsyncEnumerators.Add(memoryResults[0]);
            }

            foreach (var ret in result)
            {
                mergeAsyncEnumerators.Add(ret);
            }

            var enumerator = StreamInMemoryMerge(mergeAsyncEnumerators);
            var memory = new MemoryStreamMergeAsyncEnumerator<T>(enumerator, async);
            memoryResults.Clear();
            memoryResults.Add(memory);
            //合并
            return;
        }

        throw new ShardingInvalidOperationException($"{typeof(T)} is not {typeof(IStreamMergeEnumerator<T>)}");
    }

    public virtual IStreamMergeEnumerator<T> StreamMerge(List<IStreamMergeEnumerator<T>> parallelResults)
    {
        //如果是group in memory merger需要在内存中聚合好所有的 并且最后通过内存聚合在发挥
        if (Context.GroupMemoryMerge)
        {
            var enumerator = new AggregateStreamMergeEnumerator<T>(Context, parallelResults);
            //内存按key聚合好之后需要进行重排序按order
            var memory = new MemoryGroupStreamMergeAsyncEnumerator<T>(Context, enumerator, async);
            if (Context.PagedQuery)
            {
                //分页的前提下还需要进行内存分页
                return new PagedStreamMergeEnumerator<T>(Context, [memory]);
            }

            return memory;
        }

        if (Context.PagedQuery)
        {
            return new PagedStreamMergeEnumerator<T>(Context, parallelResults);
        }

        if (Context.HasGroup)
        {
            return new AggregateStreamMergeEnumerator<T>(Context, parallelResults);
        }

        return new OrderStreamMergeEnumerator<T>(Context, parallelResults);
    }

    protected virtual IStreamMergeEnumerator<T> StreamInMemoryMerge(List<IStreamMergeEnumerator<T>> parallelResults)
    {
        //如果是group in memory merger需要在内存中聚合好所有的 并且最后通过内存聚合在发挥
        if (Context.GroupMemoryMerge)
        {
            return new AggregateStreamMergeEnumerator<T>(Context, parallelResults);
        }

        if (Context.PagedQuery)
        {
            return new PagedStreamMergeEnumerator<T>(Context, parallelResults, 0, Context.PagedTake);//内存聚合分页不可以直接获取skip必须获取skip+take的数目
        }

        return StreamMerge(parallelResults);
    }
}

internal sealed class AllShardingMerger<T> : IShardingMerger<bool>
{
    public void MemoryMerge(List<bool> memoryResults, List<bool> parallelResults)
    {
        memoryResults.AddRange(parallelResults);
    }

    public bool StreamMerge(List<bool> parallelResults)
    {
        return parallelResults.All(o => o);
    }
}

internal sealed class AnyShardingMerger<T> : IShardingMerger<bool>
{
    public void MemoryMerge(List<bool> memoryResults, List<bool> parallelResults)
    {
        memoryResults.AddRange(parallelResults);
    }

    public bool StreamMerge(List<bool> parallelResults)
    {
        // 原先 IsNotNull() 只要有分片结果（哪怕全是 false）就返回 true
        return parallelResults.Any(static o => o);
    }
}

internal sealed class AverageShardingMerger<T> : IShardingMerger<RouteQueryResult<AverageResult<T>>>
{
    public RouteQueryResult<AverageResult<T>> StreamMerge(List<RouteQueryResult<AverageResult<T>>> parallelResults)
    {
        if (parallelResults.IsNull())
        {
            throw new InvalidOperationException("Sequence contains no elements.");
        }

        var queryable = parallelResults.Where(o => o.HasResult).Select(o => new
        {
            o.Result.Sum,
            o.Result.Count
        }).AsQueryable();

        var sum = queryable.SumBy<T>(nameof(AverageResult<object>.Sum));
        var count = queryable.Sum(o => o.Count);
        return new RouteQueryResult<AverageResult<T>>(null, null, new AverageResult<T>(sum!, count));
    }

    public void MemoryMerge(List<RouteQueryResult<AverageResult<T>>> memoryResults, List<RouteQueryResult<AverageResult<T>>> parallelResults)
    {
        memoryResults.AddRange(parallelResults);
    }
}

internal sealed class CountShardingMerger(StreamMergeContext context) : IShardingMerger<RouteQueryResult<int>>
{
    private readonly IShardingPageManager _manager = context.RuntimeContext.PageManager;

    public RouteQueryResult<int> StreamMerge(List<RouteQueryResult<int>> parallelResults)
    {

        if (_manager.Current != null)
        {
            var r = 0;
            foreach (var result in parallelResults)
            {
                _manager.Current.Results.Add(new RouteQueryResult<long>(result.DataSource, result.TableRouteResult, result.Result));
                r += result.Result;
            }

            return new RouteQueryResult<int>(null, null, r, true);
        }

        var sum = parallelResults.Sum(o => o.Result);
        return new RouteQueryResult<int>(null, null, sum, true);
    }

    public void MemoryMerge(List<RouteQueryResult<int>> memoryResults, List<RouteQueryResult<int>> parallelResults)
    {
        memoryResults.AddRange(parallelResults);
    }
}

internal sealed class LongCountShardingMerger(StreamMergeContext context) : IShardingMerger<RouteQueryResult<long>>
{
    private readonly IShardingPageManager _manager = context.RuntimeContext.PageManager;

    public RouteQueryResult<long> StreamMerge(List<RouteQueryResult<long>> parallelResults)
    {
        if (_manager.Current != null)
        {
            var r = 0L;
            foreach (var result in parallelResults)
            {
                _manager.Current.Results.Add(new RouteQueryResult<long>(result.DataSource, result.TableRouteResult, result.Result));
                r += result.Result;
            }

            return new RouteQueryResult<long>(null, null, r, true);
        }

        var sum = parallelResults.Sum(o => o.Result);
        return new RouteQueryResult<long>(null, null, sum, true);
    }

    public void MemoryMerge(List<RouteQueryResult<long>> memoryResults, List<RouteQueryResult<long>> parallelResults)
    {
        memoryResults.AddRange(parallelResults);
    }
}

internal sealed class MaxShardingMerger<T> : IShardingMerger<RouteQueryResult<T>>
{
    public RouteQueryResult<T> StreamMerge(List<RouteQueryResult<T>> parallelResults)
    {
        var results = parallelResults.FindAll(o => o.HasResult);
        if (results.IsNull())
        {
            throw new InvalidOperationException("Sequence contains no elements.");
        }

        var max = results.Max(o => o.Result);
        return new RouteQueryResult<T>(null, null, max);
    }

    public void MemoryMerge(List<RouteQueryResult<T>> memoryResults,
        List<RouteQueryResult<T>> parallelResults)
    {
        memoryResults.AddRange(parallelResults);
    }
}

internal sealed class MinShardingMerger<T> : IShardingMerger<RouteQueryResult<T>>
{
    public RouteQueryResult<T> StreamMerge(List<RouteQueryResult<T>> parallelResults)
    {
        var results = parallelResults.FindAll(o => o.HasResult);
        if (results.IsNull())
        {
            throw new InvalidOperationException("Sequence contains no elements.");
        }

        var min = results.Min(o => o.Result);
        return new RouteQueryResult<T>(null, null, min);
    }

    public void MemoryMerge(List<RouteQueryResult<T>> memoryResults,
        List<RouteQueryResult<T>> parallelResults)
    {
        memoryResults.AddRange(parallelResults);
    }
}

internal sealed class SumMethodShardingMerger<T> : IShardingMerger<RouteQueryResult<T>>
{
    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    private static T GetSumResult<TSelect>(List<TSelect> source)
    {
        if (source.IsNull())
        {
            return default!;
        }

        var sum = source.AsQueryable().SumBy(nameof(RouteQueryResult<T>.Result));
        return ConvertSum(sum);
    }

    [return: System.Diagnostics.CodeAnalysis.MaybeNull]
    private static T ConvertSum<TNumber>(TNumber number)
    {
        if (number == null)
        {
            return default!;
        }

        var convertExpr = Expression.Convert(Expression.Constant(number), typeof(T));
        return Expression.Lambda<Func<T>>(convertExpr).Compile()();
    }

    public RouteQueryResult<T> StreamMerge(List<RouteQueryResult<T>> parallelResults)
    {
        var sumResult = GetSumResult(parallelResults);
        return new RouteQueryResult<T>(null, null, sumResult, true);
    }

    public void MemoryMerge(List<RouteQueryResult<T>> memoryResults, List<RouteQueryResult<T>> parallelResults)
    {
        memoryResults.AddRange(parallelResults);
    }
}

/// <summary>
/// 和普通的不同因为是顺序查询所以需要忽略分页合并
/// </summary>
/// <typeparam name="T"></typeparam>
internal sealed class OrderEnumerableShardingMerger<T>(StreamMergeContext streamMergeContext, bool async) : EnumerableShardingMerger<T>(streamMergeContext, async)
{
    public override IStreamMergeEnumerator<T> StreamMerge(List<IStreamMergeEnumerator<T>> parallelResults)
    {
        if (Context.HasGroup)
        {
            return new AggregateStreamMergeEnumerator<T>(Context, parallelResults);
        }

        return new OrderStreamMergeEnumerator<T>(Context, parallelResults);
    }

    protected override IStreamMergeEnumerator<T> StreamInMemoryMerge(List<IStreamMergeEnumerator<T>> parallelResults)
    {
        //如果是group in memory merger需要在内存中聚合好所有的 并且最后通过内存聚合在发挥
        if (Context.GroupMemoryMerge)
        {
            return new AggregateStreamMergeEnumerator<T>(Context, parallelResults);
        }

        return StreamMerge(parallelResults);
    }
}

internal sealed class ReverseEnumerableShardingMerger<T>(StreamMergeContext context, bool async) : EnumerableShardingMerger<T>(context, async)
{
    public override IStreamMergeEnumerator<T> StreamMerge(List<IStreamMergeEnumerator<T>> parallelResults)
    {
        var enumerator = base.StreamMerge(parallelResults);
        return new MemoryReverseStreamMergeAsyncEnumerator<T>(enumerator);
    }
}

internal sealed class SequenceEnumerableShardingMerger<T>(StreamMergeContext context, bool async) : EnumerableShardingMerger<T>(context, async)
{
    public override IStreamMergeEnumerator<T> StreamMerge(List<IStreamMergeEnumerator<T>> parallelResults)
    {
        if (Context.HasGroup)
        {
            return new AggregateStreamMergeEnumerator<T>(Context,
                parallelResults);
        }

        return new OrderStreamMergeEnumerator<T>(Context, parallelResults);
    }

    protected override IStreamMergeEnumerator<T> StreamInMemoryMerge(List<IStreamMergeEnumerator<T>> parallelResults)
    {
        //如果是group in memory merger需要在内存中聚合好所有的 并且最后通过内存聚合在发挥
        if (Context.HasGroup)
        {
            return new AggregateStreamMergeEnumerator<T>(Context, parallelResults);
        }

        return StreamMerge(parallelResults);
    }
}

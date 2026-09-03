namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 断路器
/// </summary>
internal interface IMergeCircuitBreaker
{
    /// <summary>
    /// 是否拉闸
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="items"></param>
    /// <returns></returns>
    bool Terminated<T>(IEnumerable<T> items);

    /// <summary>
    /// 跳闸
    /// </summary>
    void Terminate();

    /// <summary>
    /// 注册拉闸后事件
    /// </summary>
    /// <param name="trip"></param>
    void Register(Action trip);
}

internal abstract class MergeCircuitBreaker(StreamMergeContext context) : IMergeCircuitBreaker
{
    private const int UnTriped = 0;
    private const int Triped = 1;

    private Action? _trip;
    private int _tried = UnTriped;

    protected StreamMergeContext Context { get; } = context;

    public void Register(Action trip)
    {
        _trip = trip;
    }

    public void Terminate()
    {
        _tried = Triped;
        _trip?.Invoke();
    }

    public bool Terminated<T>(IEnumerable<T> items)
    {
        if (_tried == Triped)
        {
            return true;
        }

        var tag = TerminatedCore(items);

        if (tag)
        {
            Terminate();
            return true;
        }

        return false;
    }

    protected abstract bool TerminatedCore<T>(IEnumerable<T> items);
}

internal sealed class AllMergeCircuitBreaker(StreamMergeContext context) : MergeCircuitBreaker(context)
{
    protected override bool TerminatedCore<T>(IEnumerable<T> items)
    {
        //只要有一个是false就拉闸
        return items.Any(t => t is false);
    }
}

internal sealed class AnyMergeCircuitBreaker(StreamMergeContext context) : MergeCircuitBreaker(context)
{
    protected override bool TerminatedCore<T>(IEnumerable<T> items)
    {
        return items.Any(t => t is true);
    }
}

internal sealed class EmptyMergeCircuitBreaker(StreamMergeContext context) : MergeCircuitBreaker(context)
{
    protected override bool TerminatedCore<T>(IEnumerable<T> items)
    {
        return false;
    }
}

internal sealed class AnyRouteMergeCircuitBreaker(StreamMergeContext context) : MergeCircuitBreaker(context)
{
    protected override bool TerminatedCore<T>(IEnumerable<T> items)
    {
        if (!Context.Sequence)
        {
            return false;
        }

        //只要存在任意一个结果那么就直接停止
        return items.Any(t => t is IRouteQueryResult result && result.HasResult);
    }
}

internal sealed class EnumerableMergeCircuitBreaker(StreamMergeContext context) : MergeCircuitBreaker(context)
{
    protected override bool TerminatedCore<T>(IEnumerable<T> items)
    {
        if (!Context.Sequence)
        {
            return false;
        }

        var take = Context.Take;
        if (take.HasValue)
        {
            return take.Value + Context.Skip.GetValueOrDefault() <= items.Sum(o =>
            {
                if (o is IMemoryStreamMergeAsyncEnumerator enumerator)
                {
                    return enumerator.ReallyCount;
                }

                return 0;
            });
        }

        return false;
    }
}

using BeniceSoft.Core;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IEnsureMerge<T>
{
    /// <summary>
    /// 合并结果（OrDefault / Max / Min 等在空序列时可能为 null）
    /// </summary>
    /// <returns></returns>
    [return: MaybeNull]
    T Merge();

    /// <summary>
    /// 合并结果（OrDefault / Max / Min 等在空序列时可能为 null；由实现以 T 形式返回 default）
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<T> MergeAsync(CancellationToken cancellationToken = default);
}

internal sealed class FirstEnsureMerge<T>(StreamMergeContext context) : IEnsureMerge<T>
{
    private readonly StreamMergeContext _context = context;

    public T Merge()
    {
        //将take改成1
        var enumable = new StreamMergeEnumerable<T>(_context);
        var list = enumable.ToList();
        return list.First();
    }

    public async Task<T> MergeAsync(CancellationToken cancellationToken = default)
    {
        //将take改成1
        var enumable = new StreamMergeEnumerable<T>(_context);
        var take = _context.Take;
        var list = await enumable.ToListAsync(take, cancellationToken);
        return list.First();
    }
}

internal sealed class FirstOrDefaultEnsureMerge<T>(StreamMergeContext context) : IEnsureMerge<T>
{
    private readonly StreamMergeContext _context = context;

    [return: MaybeNull]
    public T Merge()
    {
        //将take改成1
        var enumable = new StreamMergeEnumerable<T>(_context);
        var list = enumable.ToList();
        return list.FirstOrDefault()!;
    }

    public async Task<T> MergeAsync(CancellationToken cancellationToken = default)
    {
        //将take改成1
        var enumable = new StreamMergeEnumerable<T>(_context);
        var take = _context.Take;
        var list = await enumable.ToListAsync(take, cancellationToken);
        return list.FirstOrDefault()!;
    }
}

internal sealed class SingleOrDefaultEnsureMerge<T>(StreamMergeContext context) : IEnsureMerge<T>
{
    private readonly StreamMergeContext _context = context;

    [return: MaybeNull]
    public T Merge()
    {
        //将take改成1
        var enumable = new StreamMergeEnumerable<T>(_context);
        var list = enumable.ToList();
        return list.SingleOrDefault()!;
    }

    public async Task<T> MergeAsync(CancellationToken cancellationToken = default)
    {
        //将take改成1
        var enumable = new StreamMergeEnumerable<T>(_context);
        var take = _context.Take;
        var list = await enumable.ToListAsync(take, cancellationToken);
        return list.SingleOrDefault()!;
    }
}

internal sealed class SingleEnsureMerge<T>(StreamMergeContext context) : IEnsureMerge<T>
{
    private readonly StreamMergeContext _context = context;

    public T Merge()
    {
        //将take改成1
        var enumable = new StreamMergeEnumerable<T>(_context);
        var list = enumable.ToList();
        return list.Single();
    }

    public async Task<T> MergeAsync(CancellationToken cancellationToken = default)
    {
        //将take改成1
        var enumable = new StreamMergeEnumerable<T>(_context);
        var take = _context.Take;
        var list = await enumable.ToListAsync(take, cancellationToken);
        return list.Single();
    }
}

internal sealed class LastEnsureMerge<T>(StreamMergeContext context) : IEnsureMerge<T>
{
    private readonly StreamMergeContext _context = context;

    public T Merge()
    {
        var count = _context.Skip.GetValueOrDefault() + 1;
        //将take改成1
        var enumable = new StreamMergeEnumerable<T>(_context);
        var list = Enumerable.Take(enumable, count).ToList();
        if (list.Count >= count)
        {
            return list[0];
        }

        throw new ShardingException("Sequence contains no elements.");
    }

    public async Task<T> MergeAsync(CancellationToken cancellationToken = default)
    {

        var count = _context.Skip.GetValueOrDefault() + 1;
        //将take改成1
        var enumable = new StreamMergeEnumerable<T>(_context);
        var list = await enumable.ToListAsync(count, cancellationToken);

        if (list.Count >= count)
        {
            return list[0];
        }

        throw new ShardingException("Sequence contains no elements.");
    }
}

internal sealed class LastOrDefaultEnsureMerge<T>(StreamMergeContext context) : IEnsureMerge<T>
{
    private readonly StreamMergeContext _context = context;

    [return: MaybeNull]
    public T Merge()
    {
        var count = _context.Skip.GetValueOrDefault() + 1;
        //将take改成1
        var enumable = new StreamMergeEnumerable<T>(_context);
        var list = Enumerable.Take(enumable, count).ToList();
        if (list.Count >= count)
        {
            return list.FirstOrDefault()!;
        }

        return default!;
    }

    public async Task<T> MergeAsync(CancellationToken cancellationToken = default)
    {

        var count = _context.Skip.GetValueOrDefault() + 1;
        //将take改成1
        var enumable = new StreamMergeEnumerable<T>(_context);
        var list = await enumable.ToListAsync(count, cancellationToken);
        if (list.Count >= count)
        {
            return list.FirstOrDefault()!;
        }

        return default!;
    }
}

internal abstract class BaseMerge(StreamMergeContext context)
{
    public StreamMergeContext Context { get; } = context;

    protected virtual IEnumerable<ISqlRouteUnit> GetRouteUnits()
    {
        if (Context.UseMerge)
        {
            return Context.RouteResult.RouteUnits.GroupBy(o => o.DataSource).Select(o => new EmptySqlRouteUnit(o.Key, o.Select(g => g.RouteResult).ToList()));
        }

        return Context.RouteResult.RouteUnits;
    }
}

internal abstract class BaseEnsureMerge<T, TResult>(StreamMergeContext context) : BaseMerge(context), IEnsureMerge<TResult>
{
    [return: MaybeNull]
    public TResult Merge()
    {
        return MergeAsync().GetAwaiter().GetResult()!;
    }

    public async Task<TResult> MergeAsync(CancellationToken cancellationToken = default)
    {
        var resultType = typeof(T);
        if (!resultType.IsNullableType())
        {
            if (typeof(decimal) == resultType)
            {
                var result = await ExecuteAsync<decimal?>(cancellationToken);
                return ConvertNumber(result)!;
            }

            if (typeof(float) == resultType)
            {
                var result = await ExecuteAsync<float?>(cancellationToken);
                return ConvertNumber(result)!;
            }

            if (typeof(int) == resultType)
            {
                var result = await ExecuteAsync<int?>(cancellationToken);
                return ConvertNumber(result)!;
            }

            if (typeof(long) == resultType)
            {
                var result = await ExecuteAsync<long?>(cancellationToken);
                return ConvertNumber(result)!;
            }

            if (typeof(double) == resultType)
            {
                var result = await ExecuteAsync<double?>(cancellationToken);
                return ConvertNumber(result)!;
            }

            throw new ShardingException($"cant calc min value, type:[{resultType}]");
        }
        else
        {
            var result = await ExecuteAsync<TResult>(cancellationToken);
            return result!;
        }
    }

    private async Task<TR?> ExecuteAsync<TR>(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Context.TryPrepareExecute(() => default(TR), out var tr))
        {
            return tr;
        }

        var units = GetRouteUnits();
        var executor = GetExecutor<TR>();
        var result = await ShardingMergeExecutor.ExecuteAsync<RouteQueryResult<TR>>(Context, executor, true, units, cancellationToken);
        return result.Result;
    }

    [return: MaybeNull]
    private static TResult ConvertNumber<TNumber>(TNumber? number)
    {
        if (number is null)
        {
            return default!;
        }

        var convertExpr = Expression.Convert(Expression.Constant(number, typeof(TNumber)), typeof(TResult));
        return Expression.Lambda<Func<TResult>>(convertExpr).Compile()();
    }

    private IMergeExecutor<RouteQueryResult<TR>> GetExecutor<TR>()
    {
        var resultType = typeof(T);
        if (!resultType.IsNullableType())
        {
            if (typeof(decimal) == resultType)
            {
                return GetExecutor<decimal?, TR>();
            }

            if (typeof(float) == resultType)
            {
                return GetExecutor<float?, TR>();
            }

            if (typeof(int) == resultType)
            {
                return GetExecutor<int?, TR>();
            }

            if (typeof(long) == resultType)
            {
                return GetExecutor<long?, TR>();
            }

            if (typeof(double) == resultType)
            {
                return GetExecutor<double?, TR>();
            }

            throw new ShardingException($"cant calc max value, type:[{resultType}]");
        }
        else
        {
            return GetExecutor<T, TR>();
        }
    }

    protected abstract IMergeExecutor<RouteQueryResult<TR>> GetExecutor<TS, TR>();
}

internal sealed class MaxEnsureMerge<T, TResult>(StreamMergeContext context) : BaseEnsureMerge<T, TResult>(context)
{
    protected override IMergeExecutor<RouteQueryResult<TR>> GetExecutor<TS, TR>()
    {
        return (IMergeExecutor<RouteQueryResult<TR>>)(object)new MaxMethodExecutor<T, TS>(Context);
    }
}

internal sealed class MinEnsureMerge<T, TResult>(StreamMergeContext context) : BaseEnsureMerge<T, TResult>(context)
{
    protected override IMergeExecutor<RouteQueryResult<TR>> GetExecutor<TS, TR>()
    {
        return (IMergeExecutor<RouteQueryResult<TR>>)(object)new MinMethodExecutor<T, TS>(Context);
    }
}

internal abstract class EnsureMerge<T>(StreamMergeContext context) : BaseMerge(context), IEnsureMerge<T>
{
    public T Merge()
    {
        return MergeAsync().GetResult();
    }

    public async Task<T> MergeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Context.TryPrepareExecute(() => default(T)!, out var enumerator))
        {
            return enumerator!;
        }

        var units = GetRouteUnits();
        var executor = GetExecutor();
        var result = await ShardingMergeExecutor.ExecuteAsync<T>(Context, executor, true, units, cancellationToken);
        return result;
    }

    protected abstract IMergeExecutor<T> GetExecutor();
}

internal abstract class WrapEnsureMerge<T>(StreamMergeContext context) : BaseMerge(context), IEnsureMerge<T>
{
    public T Merge()
    {
        // Count/Sum/ExecuteUpdate/ExecuteDelete 仅支持异步；同步入口由 EF 触发，明确拒绝以免 NotImplemented 误导
        throw new ShardingNotSupportException(
            $"Synchronous {Context.MergeContext.Name} is not supported for sharding queries. Use the Async overload (e.g. CountAsync / SumAsync / ExecuteDeleteAsync / ExecuteUpdateAsync).");
    }

    public async Task<T> MergeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Context.TryPrepareExecute(() => default(T)!, out var enumerator))
        {
            return enumerator!;
        }

        var units = GetRouteUnits();
        var executor = GetExecutor();
        var result = await ShardingMergeExecutor.ExecuteAsync<RouteQueryResult<T>>(Context,
            executor, true, units, cancellationToken);
        return result.Result;
    }

    protected abstract IMergeExecutor<RouteQueryResult<T>> GetExecutor();
}

internal sealed class AverageAsyncInMemoryMergeEngine<T, TResult, TSelect>(StreamMergeContext context) : BaseMerge(context), IEnsureMerge<TResult>
{
    public TResult Merge()
    {
        return MergeAsync().GetResult();
    }

    public async Task<TResult> MergeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Context.TryPrepareExecute(() => default(TResult)!, out var enumerator))
        {
            return enumerator!;
        }

        var units = GetRouteUnits();
        var executor = new AverageMethodExecutor<TSelect>(Context);
        var result = await ShardingMergeExecutor.ExecuteAsync(Context, executor, true, units, cancellationToken);
        var sum = result.Result.Sum;
        var count = result.Result.Count;
        return sum.AverageConstant<TSelect, long, TResult>(count);
    }
}

internal sealed class AllEnsureMerge<T>(StreamMergeContext context) : EnsureMerge<bool>(context)
{
    protected override IMergeExecutor<bool> GetExecutor()
    {
        return new AllMethodExecutor<T>(Context);
    }
}

internal sealed class AnyEnsureMerge<T>(StreamMergeContext context) : EnsureMerge<bool>(context)
{
    protected override IMergeExecutor<bool> GetExecutor()
    {
        return new AnyMethodExecutor<T>(Context);
    }
}

internal sealed class ContainsEnsureMerge<T>(StreamMergeContext context) : EnsureMerge<bool>(context)
{
    protected override IMergeExecutor<bool> GetExecutor()
    {
        return new ContainsMethodExecutor<T>(Context);
    }
}

internal sealed class CountEnsureMerge<T>(StreamMergeContext context) : WrapEnsureMerge<int>(context)
{
    protected override IMergeExecutor<RouteQueryResult<int>> GetExecutor()
    {
        return new CountMethodExecutor<T>(Context);
    }
}

internal sealed class LongCountEnsureMerge<T>(StreamMergeContext context) : WrapEnsureMerge<long>(context)
{
    protected override IMergeExecutor<RouteQueryResult<long>> GetExecutor()
    {
        return new LongCountMethodExecutor<T>(Context);
    }
}

internal sealed class DeleteEnsureMerge<T>(StreamMergeContext context) : WrapEnsureMerge<int>(context)
{
    protected override IMergeExecutor<RouteQueryResult<int>> GetExecutor()
    {
        return new DeleteMethodExecutor<T>(Context);
    }
}

internal sealed class UpdateEnsureMerge<T>(StreamMergeContext context) : WrapEnsureMerge<int>(context)
{
    protected override IMergeExecutor<RouteQueryResult<int>> GetExecutor()
    {
        return new UpdateMethodExecutor<T>(Context);
    }
}

internal sealed class SumEnsureMerge<T, TResult>(StreamMergeContext context) : WrapEnsureMerge<TResult>(context)
{
    protected override IMergeExecutor<RouteQueryResult<TResult>> GetExecutor()
    {
        return new SumMethodExecutor<TResult>(Context);
    }
}

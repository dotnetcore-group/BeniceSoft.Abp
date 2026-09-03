using BeniceSoft.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IMergeExecutor<T>
{
    IShardingMerger<T> GetShardingMerger();

    Task<List<T>> ExecuteAsync(bool async, DataSourceMergerSqlUnit sqlUnit, CancellationToken cancellationToken = default);
}

internal abstract class MergeExecutor<T> : IMergeExecutor<T>
{
    private const int Cancelled = 0;
    private const int NotCancelled = 1;

    private int _status = NotCancelled;

    protected MergeExecutor(StreamMergeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Context = context;
    }

    protected StreamMergeContext Context { get; }

    public Task<List<T>> ExecuteAsync(bool async, DataSourceMergerSqlUnit sqlUnit, CancellationToken cancellationToken = default)
    {
        return ExecuteCoreAsync(sqlUnit, cancellationToken);
    }

    public abstract IShardingMerger<T> GetShardingMerger();

    protected void Cancel()
    {
        Interlocked.Exchange(ref _status, Cancelled);
    }

    protected abstract Task<ShardingMergeResult<T>> ExecuteAsync(MergerSqlUnit unit, CancellationToken cancellationToken = default);

    /// <summary>
    /// 同库同组下面的并行异步执行，需要归并成一个结果
    /// </summary>
    /// <param name="units"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<List<ShardingMergeResult<T>>> GroupExecuteAsync(IReadOnlyList<MergerSqlUnit> units, CancellationToken cancellationToken = default)
    {
        if (units.Count <= 0)
        {
            return [];
        }
        else
        {
            var tasks = units.Select(unit => ExecuteAsync(unit, cancellationToken)).ToArray();

            var results = await TaskHelper.WhenAllFastFail(tasks);
            var result = results.ToList();

            return result;
        }
    }

    public abstract IMergeCircuitBreaker GetCircuitBreaker();

    private async Task<List<T>> ExecuteCoreAsync(DataSourceMergerSqlUnit unit, CancellationToken cancellationToken = default)
    {
        var items = new List<T>();
        var policy = GetCircuitBreaker();
        var count = unit.Groups.Count;
        //同数据库下多组数据间采用串行
        foreach (var group in unit.Groups)
        {
            count--;
            //同组采用并行最大化用户配置链接数
            var results = await GroupExecuteAsync(group.Groups, cancellationToken);
            //严格限制连接数就在内存中进行聚合并且直接回收掉当前dbcontext
            if (unit.ConnectionMode == ConnectionMode.ConnectionStrictly)
            {
                GetShardingMerger().MemoryMerge(items, results.Select(o => o.Result).ToList());
                // MergeParallelExecuteResult(result, , async);
                foreach (var routeQueryResult in results)
                {
                    var db = routeQueryResult.DbContext;
                    if (db != null)
                    {
                        await Context.DisposeAsync(db);
                    }
                }
            }
            else
            {
                foreach (var routeQueryResult in results)
                {
                    items.Add(routeQueryResult.Result);
                }
            }

            //是否存在下次轮询如果是的那么就需要判断
            var hasNextLoop = count > 0;
            if (hasNextLoop)
            {
                if (_status == Cancelled || policy.Terminated(items))
                {
                    break;
                }
            }
        }

        return items;
    }
}

internal abstract class EnumerableExecutor<T>(StreamMergeContext context) : MergeExecutor<IStreamMergeEnumerator<T>>(context)
{
    public override IMergeCircuitBreaker GetCircuitBreaker()
    {
        return new EnumerableMergeCircuitBreaker(Context);
    }

    protected static async Task<IStreamMergeEnumerator<T>> GetParallelEnumerator(IQueryable<T> queryable, bool async, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (async)
        {
            var enumator = queryable.AsAsyncEnumerable().GetAsyncEnumerator(cancellationToken);
            await enumator.MoveNextAsync();
            return new StreamMergeEnumerator<T>(enumator);
        }
        else
        {
            var enumator = queryable.AsEnumerable().GetEnumerator();
            enumator.MoveNext();
            return new StreamMergeEnumerator<T>(enumator);
        }
    }

    /// <summary>
    /// 是否需要在执行单元中直接回收掉链接有助于提高吞吐量
    /// </summary>
    /// <param name="context"></param>
    /// <param name="enumerator"></param>
    /// <returns></returns>
    private bool Dispose<TResult>(StreamMergeContext context, IStreamMergeEnumerator<TResult> enumerator)
    {
        var name = context.MergeContext.Name;
        var hasElement = enumerator.HasElement;
        switch (name)
        {
            case nameof(Queryable.First):
            case nameof(Queryable.FirstOrDefault):
            case nameof(Queryable.Last):
            case nameof(Queryable.LastOrDefault):
                {
                    var skip = context.Skip;
                    return !hasElement || skip is null or < 0;
                }
            case nameof(Queryable.Single):
            case nameof(Queryable.SingleOrDefault):
            case QueryCompilerContext.Enumerable:
                {
                    return !hasElement;
                }
        }

        return false;
    }

    protected override async Task<ShardingMergeResult<IStreamMergeEnumerator<T>>> ExecuteAsync(MergerSqlUnit unit, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteCoreAsync(unit, cancellationToken);
        var ctx = result.DbContext;
        var mergeResult = result.Result;

        //连接数严格的会在内存中聚合然后聚合后回收,非连接数严格需要判断是否需要当前执行单元直接回收
        //first last 等操作没有skip就可以回收，如果没有元素就可以回收
        //single如果没有元素就可以回收
        //enumerable如果没有元素就可以回收
        if (Dispose(Context, mergeResult))
        {
            var enumerator = new MostStreamMergeEnumerator<T>(mergeResult);
            await Context.DisposeAsync(ctx);
            return new ShardingMergeResult<IStreamMergeEnumerator<T>>(null, enumerator);
        }

        return result;
    }

    protected abstract Task<ShardingMergeResult<IStreamMergeEnumerator<T>>> ExecuteCoreAsync(MergerSqlUnit unit, CancellationToken cancellationToken = default);
}

internal abstract class MethodExecutor<T>(StreamMergeContext context) : MergeExecutor<T>(context)
{
    protected override async Task<ShardingMergeResult<T>> ExecuteAsync(MergerSqlUnit unit, CancellationToken cancellationToken = default)
    {
        var db = Context.CreateDbContext(unit.RouteUnit);
        var newQueryable = Context.RewriteQueryable.ReplaceDbContextQueryable(db);

        var queryResult = await ExecuteAsync(newQueryable, cancellationToken);
        await Context.DisposeAsync(db);
        return new ShardingMergeResult<T>(null, queryResult);
    }

    protected abstract Task<T> ExecuteAsync(IQueryable queryable, CancellationToken cancellationToken = default);
}

internal abstract class WrapMethodExecutor<T>(StreamMergeContext context) : MergeExecutor<RouteQueryResult<T>>(context)
{
    protected override async Task<ShardingMergeResult<RouteQueryResult<T>>> ExecuteAsync(MergerSqlUnit unit, CancellationToken cancellationToken = default)
    {
        var dataSource = unit.RouteUnit.DataSource;
        var routeResult = unit.RouteUnit.RouteResult;

        var db = Context.CreateDbContext(unit.RouteUnit);
        var newQueryable = Context.RewriteQueryable.ReplaceDbContextQueryable(db);

        var queryResult = await ExecuteAsync(newQueryable, cancellationToken);
        var routeQueryResult = new RouteQueryResult<T>(dataSource, routeResult, queryResult);
        await Context.DisposeAsync(db);
        return new ShardingMergeResult<RouteQueryResult<T>>(null, routeQueryResult);
    }

    /// <summary>
    /// 分片执行；Average 等在无数据时可通过 <see cref="RouteQueryResult{T}"/> 的 null Result 表示无结果。
    /// </summary>
    protected abstract Task<T> ExecuteAsync(IQueryable queryable, CancellationToken cancellationToken = default);
}

internal sealed class AllMethodExecutor<T>(StreamMergeContext context) : MethodExecutor<bool>(context)
{
    private static readonly AllShardingMerger<T> _merger = new();

    public override IMergeCircuitBreaker GetCircuitBreaker()
    {
        var breaker = new AllMergeCircuitBreaker(Context);
        breaker.Register(Cancel);
        return breaker;
    }

    public override IShardingMerger<bool> GetShardingMerger()
    {
        return _merger;
    }

    protected override Task<bool> ExecuteAsync(IQueryable queryable, CancellationToken cancellationToken = default)
    {
        var result = (AllQueryCombineResult)Context.MergeContext.Result;
        Expression<Func<T, bool>> allPredicate = x => true;
        var predicate = result.Expression;
        if (predicate != null)
        {
            allPredicate = (Expression<Func<T, bool>>)predicate;
        }

        return ((IQueryable<T>)queryable).AllAsync(allPredicate, cancellationToken);
    }
}

internal sealed class AnyMethodExecutor<T>(StreamMergeContext context) : MethodExecutor<bool>(context)
{
    private static readonly AnyShardingMerger<T> _merger = new();

    public override IMergeCircuitBreaker GetCircuitBreaker()
    {
        var breaker = new AnyMergeCircuitBreaker(Context);
        breaker.Register(Cancel);
        return breaker;
    }

    public override IShardingMerger<bool> GetShardingMerger()
    {
        return _merger;
    }

    protected override Task<bool> ExecuteAsync(IQueryable queryable, CancellationToken cancellationToken = default)
    {
        return ((IQueryable<T>)queryable).AnyAsync(cancellationToken);
    }
}

internal sealed class ContainsMethodExecutor<T>(StreamMergeContext context) : MethodExecutor<bool>(context)
{
    private static readonly AnyShardingMerger<T> _merger = new();

    public override IMergeCircuitBreaker GetCircuitBreaker()
    {
        var breaker = new AnyMergeCircuitBreaker(Context);
        breaker.Register(Cancel);
        return breaker;
    }

    public override IShardingMerger<bool> GetShardingMerger()
    {
        return _merger;
    }

    protected override Task<bool> ExecuteAsync(IQueryable queryable, CancellationToken cancellationToken = default)
    {
        var result = (ConstantQueryCombineResult)Context.MergeContext.Result;
        var constantItem = (T)result.Constant!;
        return ((IQueryable<T>)queryable).ContainsAsync(constantItem, cancellationToken);
    }
}

internal sealed class AverageMethodExecutor<T>(StreamMergeContext context) : WrapMethodExecutor<AverageResult<T>>(context)
{
    public override IMergeCircuitBreaker GetCircuitBreaker()
    {
        var breaker = new EmptyMergeCircuitBreaker(Context);
        breaker.Register(Cancel);
        return breaker;
    }

    public override IShardingMerger<RouteQueryResult<AverageResult<T>>> GetShardingMerger()
    {
        return new AverageShardingMerger<T>();
    }

    protected override async Task<AverageResult<T>> ExecuteAsync(IQueryable queryable, CancellationToken cancellationToken = default)
    {
        var count = 0L;
        T? sum = default;
        var newQueryable = (IQueryable<T>)queryable;
        var r = await newQueryable.GroupBy(o => 1).BuildExpression().FirstOrDefaultAsync(cancellationToken);
        if (r != null)
        {
            count = r.Item1;
            sum = r.Item2;
        }

        if (count <= 0)
        {
            // null → RouteQueryResult.HasResult = false，合并时跳过空分片
            return null!;
        }

        return new AverageResult<T>(sum!, count);
    }
}

internal sealed class CountMethodExecutor<T>(StreamMergeContext context) : WrapMethodExecutor<int>(context)
{
    public override IMergeCircuitBreaker GetCircuitBreaker()
    {
        var breaker = new EmptyMergeCircuitBreaker(Context);
        breaker.Register(Cancel);
        return breaker;
    }

    public override IShardingMerger<RouteQueryResult<int>> GetShardingMerger()
    {
        return new CountShardingMerger(Context);
    }

    protected override Task<int> ExecuteAsync(IQueryable queryable, CancellationToken cancellationToken = default)
    {
        return ((IQueryable<T>)queryable).CountAsync(cancellationToken);
    }
}

internal sealed class LongCountMethodExecutor<T>(StreamMergeContext context) : WrapMethodExecutor<long>(context)
{
    public override IMergeCircuitBreaker GetCircuitBreaker()
    {
        var breaker = new EmptyMergeCircuitBreaker(Context);
        breaker.Register(Cancel);
        return breaker;
    }

    public override IShardingMerger<RouteQueryResult<long>> GetShardingMerger()
    {
        return new LongCountShardingMerger(Context);
    }

    protected override Task<long> ExecuteAsync(IQueryable queryable, CancellationToken cancellationToken = default)
    {
        return ((IQueryable<T>)queryable).LongCountAsync(cancellationToken);
    }
}

internal sealed class DeleteMethodExecutor<T>(StreamMergeContext context) : WrapMethodExecutor<int>(context)
{
    public override IMergeCircuitBreaker GetCircuitBreaker()
    {
        var breaker = new EmptyMergeCircuitBreaker(Context);
        breaker.Register(Cancel);
        return breaker;
    }

    public override IShardingMerger<RouteQueryResult<int>> GetShardingMerger()
    {
        return new CountShardingMerger(Context);
    }

    protected override Task<int> ExecuteAsync(IQueryable queryable, CancellationToken cancellationToken = default)
    {
        return ((IQueryable<T>)queryable).ExecuteDeleteAsync(cancellationToken);
    }
}

internal sealed class UpdateMethodExecutor<T>(StreamMergeContext context) : WrapMethodExecutor<int>(context)
{
    public override IMergeCircuitBreaker GetCircuitBreaker()
    {
        var breaker = new EmptyMergeCircuitBreaker(Context);
        breaker.Register(Cancel);
        return breaker;
    }

    public override IShardingMerger<RouteQueryResult<int>> GetShardingMerger()
    {
        return new CountShardingMerger(Context);
    }

    protected override Task<int> ExecuteAsync(IQueryable queryable, CancellationToken cancellationToken = default)
    {
        // EF Core 10: ExecuteUpdate 接受 Action<UpdateSettersBuilder<T>>（非 Expression）
        var result = (UpdateQueryCombineResult)Context.MergeContext.Result;
        Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<T>> action = static _ => { };
        if (result.Expression is not null)
        {
            action = (Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<T>>)result.Expression.Compile();
        }

        return ((IQueryable<T>)queryable).ExecuteUpdateAsync(action, cancellationToken);
    }
}

internal sealed class MaxMethodExecutor<T, TResult>(StreamMergeContext context) : WrapMethodExecutor<TResult>(context)
{
    public override IMergeCircuitBreaker GetCircuitBreaker()
    {
        var breaker = new AnyRouteMergeCircuitBreaker(Context);
        breaker.Register(Cancel);
        return breaker;
    }

    public override IShardingMerger<RouteQueryResult<TResult>> GetShardingMerger()
    {
        return new MaxShardingMerger<TResult>();
    }

    protected override Task<TResult> ExecuteAsync(IQueryable queryable, CancellationToken cancellationToken = default)
    {
        var resultType = typeof(T);
        if (!resultType.IsNullableType())
        {
            if (typeof(decimal) == resultType)
            {
                return (Task<TResult>)(object)((IQueryable<decimal>)queryable).Select(t => (decimal?)t).MaxAsync(cancellationToken);
            }

            if (typeof(float) == resultType)
            {
                return (Task<TResult>)(object)((IQueryable<float>)queryable).Select(t => (float?)t).MaxAsync(cancellationToken);
            }

            if (typeof(int) == resultType)
            {
                return (Task<TResult>)(object)((IQueryable<int>)queryable).Select(t => (int?)t).MaxAsync(cancellationToken);
            }

            if (typeof(long) == resultType)
            {
                return (Task<TResult>)(object)((IQueryable<long>)queryable).Select(t => (long?)t).MaxAsync(cancellationToken);
            }

            if (typeof(double) == resultType)
            {
                return (Task<TResult>)(object)((IQueryable<double>)queryable).Select(t => (double?)t).MaxAsync(cancellationToken);
            }

            throw new ShardingException($"cant calc max value, type:[{resultType}]");
        }
        else
        {
            return (Task<TResult>)(object)((IQueryable<T>)queryable).MaxAsync(cancellationToken);
        }
    }
}

internal sealed class MinMethodExecutor<T, TResult>(StreamMergeContext context) : WrapMethodExecutor<TResult>(context)
{
    public override IMergeCircuitBreaker GetCircuitBreaker()
    {
        var breaker = new AnyRouteMergeCircuitBreaker(Context);
        breaker.Register(Cancel);
        return breaker;
    }

    public override IShardingMerger<RouteQueryResult<TResult>> GetShardingMerger()
    {
        return new MinShardingMerger<TResult>();
    }

    protected override Task<TResult> ExecuteAsync(IQueryable queryable, CancellationToken cancellationToken = default)
    {
        var resultType = typeof(T);
        if (!resultType.IsNullableType())
        {
            if (typeof(decimal) == resultType)
            {
                return (Task<TResult>)(object)((IQueryable<decimal>)queryable).Select(t => (decimal?)t).MinAsync(cancellationToken);
            }

            if (typeof(float) == resultType)
            {
                return (Task<TResult>)(object)((IQueryable<float>)queryable).Select(t => (float?)t).MinAsync(cancellationToken);
            }

            if (typeof(int) == resultType)
            {
                return (Task<TResult>)(object)((IQueryable<int>)queryable).Select(t => (int?)t).MinAsync(cancellationToken);
            }

            if (typeof(long) == resultType)
            {
                return (Task<TResult>)(object)((IQueryable<long>)queryable).Select(t => (long?)t).MinAsync(cancellationToken);
            }

            if (typeof(double) == resultType)
            {
                return (Task<TResult>)(object)((IQueryable<double>)queryable).Select(t => (double?)t).MinAsync(cancellationToken);
            }

            throw new ShardingException($"cant calc max value, type:[{resultType}]");
        }
        else
        {
            return (Task<TResult>)(object)((IQueryable<T>)queryable).MinAsync(cancellationToken);
        }
    }
}

internal sealed class SumMethodExecutor<T>(StreamMergeContext context) : WrapMethodExecutor<T>(context)
{
    public override IMergeCircuitBreaker GetCircuitBreaker()
    {
        var breaker = new EmptyMergeCircuitBreaker(Context);
        breaker.Register(Cancel);
        return breaker;
    }

    public override IShardingMerger<RouteQueryResult<T>> GetShardingMerger()
    {
        return new SumMethodShardingMerger<T>();
    }

    protected override Task<T> ExecuteAsync(IQueryable queryable, CancellationToken cancellationToken = default)
    {
        var resultType = typeof(T);
        if (!resultType.IsNumeric())
        {
            throw new ShardingException($"not support {Context.MergeContext.Expression.Print()} result {resultType}");
        }

        return ShardingQueryableMethods.GetSumMethod(resultType).ExecuteAsync<T, Task<T>>((IQueryable<T>)queryable, expression: null, cancellationToken);
    }
}

internal sealed class ShardingEnumerableExecutor<T>(StreamMergeContext context, bool async) : EnumerableExecutor<T>(context)
{
    private readonly bool _async = async;
    private readonly IShardingMerger<IStreamMergeEnumerator<T>> _merge = new EnumerableShardingMerger<T>(context, async);

    public override IShardingMerger<IStreamMergeEnumerator<T>> GetShardingMerger()
    {
        return _merge;
    }

    protected override async Task<ShardingMergeResult<IStreamMergeEnumerator<T>>> ExecuteCoreAsync(MergerSqlUnit unit, CancellationToken cancellationToken = default)
    {
        var db = Context.CreateDbContext(unit.RouteUnit);
        var newQueryable = (IQueryable<T>)Context.RewriteQueryable.ReplaceDbContextQueryable(db);
        var enumerator = await GetParallelEnumerator(newQueryable, _async, cancellationToken);
        return new ShardingMergeResult<IStreamMergeEnumerator<T>>(db, enumerator);
    }
}

internal sealed class LastEnumerableExecutor<T>(StreamMergeContext context, IQueryable<T> query, bool async) : EnumerableExecutor<T>(context)
{
    private readonly IQueryable<T> _query = query;
    private readonly bool _async = async;
    private readonly IShardingMerger<IStreamMergeEnumerator<T>> _merge = new EnumerableShardingMerger<T>(context, async);

    public override IShardingMerger<IStreamMergeEnumerator<T>> GetShardingMerger()
    {
        return _merge;
    }

    protected override async Task<ShardingMergeResult<IStreamMergeEnumerator<T>>> ExecuteCoreAsync(MergerSqlUnit unit, CancellationToken cancellationToken = default)
    {
        var db = Context.CreateDbContext(unit.RouteUnit);
        var newQueryable = _query.ReplaceDbContextQueryable(db);
        var enumerator = await GetParallelEnumerator(newQueryable, _async, cancellationToken);
        return new ShardingMergeResult<IStreamMergeEnumerator<T>>(db, enumerator);
    }
}

internal sealed class OrderEnumerableExecutor<T>(StreamMergeContext context, bool async) : EnumerableExecutor<T>(context)
{
    private readonly bool _async = async;
    private readonly IShardingMerger<IStreamMergeEnumerator<T>> _merge = new OrderEnumerableShardingMerger<T>(context, async);
    private readonly IQueryable<T> _query = (IQueryable<T>)context.OriginalQueryable.RemoveVisitor(nameof(Queryable.Skip), nameof(Queryable.Take));

    public override IShardingMerger<IStreamMergeEnumerator<T>> GetShardingMerger()
    {
        return _merge;
    }

    protected override async Task<ShardingMergeResult<IStreamMergeEnumerator<T>>> ExecuteCoreAsync(MergerSqlUnit unit, CancellationToken cancellationToken = default)
    {
        var routeUnit = (SequenceRouteUnit)unit.RouteUnit;
        var result = routeUnit.SequenceResult;
        var db = Context.CreateDbContext(routeUnit);
        var newQueryable = _query.Skip(result.Skip).Take(result.Take).WithSort(Context.Sorts).ReplaceDbContextQueryable(db);
        var enumerator = await GetParallelEnumerator(newQueryable, _async, cancellationToken);
        return new ShardingMergeResult<IStreamMergeEnumerator<T>>(db, enumerator);
    }
}

internal sealed class ReverseEnumerableExecutor<T>(StreamMergeContext context, IOrderedQueryable<T> query, bool async) : EnumerableExecutor<T>(context)
{
    private readonly IShardingMerger<IStreamMergeEnumerator<T>> _merge = new ReverseEnumerableShardingMerger<T>(context, async);

    public override IShardingMerger<IStreamMergeEnumerator<T>> GetShardingMerger()
    {
        return _merge;
    }

    protected override async Task<ShardingMergeResult<IStreamMergeEnumerator<T>>> ExecuteCoreAsync(MergerSqlUnit unit, CancellationToken cancellationToken = default)
    {
        var db = Context.CreateDbContext(unit.RouteUnit);
        var newQueryable = query.ReplaceDbContextQueryable(db);
        var enumerator = await GetParallelEnumerator(newQueryable, async, cancellationToken);
        return new ShardingMergeResult<IStreamMergeEnumerator<T>>(db, enumerator);
    }
}

internal sealed class SequenceEnumerableExecutor<T>(StreamMergeContext context, bool async) : EnumerableExecutor<T>(context)
{
    private readonly IShardingMerger<IStreamMergeEnumerator<T>> _merge = new SequenceEnumerableShardingMerger<T>(context, async);
    private readonly IQueryable<T> _query = (IQueryable<T>)context.OriginalQueryable.RemoveVisitor(nameof(Queryable.Skip), nameof(Queryable.Take));

    public override IShardingMerger<IStreamMergeEnumerator<T>> GetShardingMerger()
    {
        return _merge;
    }

    protected override async Task<ShardingMergeResult<IStreamMergeEnumerator<T>>> ExecuteCoreAsync(MergerSqlUnit unit, CancellationToken cancellationToken = default)
    {
        var routeUnit = (SequenceRouteUnit)unit.RouteUnit;
        var result = routeUnit.SequenceResult;
        var db = Context.CreateDbContext(routeUnit);
        var newQueryable = _query.Skip(result.Skip).Take(result.Take).ReplaceDbContextQueryable(db);
        var enumerator = await GetParallelEnumerator(newQueryable, async, cancellationToken);
        return new ShardingMergeResult<IStreamMergeEnumerator<T>>(db, enumerator);
    }
}

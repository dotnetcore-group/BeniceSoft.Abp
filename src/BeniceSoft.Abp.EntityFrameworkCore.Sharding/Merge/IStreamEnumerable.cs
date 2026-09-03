using BeniceSoft.Core.Strategy;
using System.Collections;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IStreamEnumerable<T> : IAsyncEnumerable<T>, IEnumerable<T>, IDisposable
{
}

internal abstract class StreamEnumerable<T>(StreamMergeContext context) : BaseMerge(context), IStreamEnumerable<T>, IAsyncDisposable
{
    public void Dispose()
    {
        Context.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return Context.DisposeAsync();
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return GetAsyncEnumerator(true, cancellationToken);
    }

    protected virtual IStreamMergeEnumerator<T> GetAsyncEnumerator(bool async, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var units = GetRouteUnits();
        var executor = GetExecutor(async);
        return ShardingMergeExecutor.Execute<IStreamMergeEnumerator<T>>(Context, executor, async, units, cancellationToken);
    }

    public IEnumerator<T> GetEnumerator()
    {
        return GetAsyncEnumerator(false);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    protected abstract IMergeExecutor<IStreamMergeEnumerator<T>> GetExecutor(bool async);
}

internal sealed class ShardingStreamEnumerable<T>(StreamMergeContext context) : StreamEnumerable<T>(context)
{
    protected override IMergeExecutor<IStreamMergeEnumerator<T>> GetExecutor(bool async)
    {
        return new ShardingEnumerableExecutor<T>(Context, async);
    }
}

internal sealed class EmptyStreamEnumerable<T>(StreamMergeContext context) : StreamEnumerable<T>(context)
{
    protected override IStreamMergeEnumerator<T> GetAsyncEnumerator(bool async, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var asyncEnumerator = new EmptyQueryEnumerator<T>();
        if (async)
        {
            return new StreamMergeEnumerator<T>((IAsyncEnumerator<T>)asyncEnumerator);
        }
        else
        {
            return new StreamMergeEnumerator<T>((IEnumerator<T>)asyncEnumerator);
        }
    }

    protected override IMergeExecutor<IStreamMergeEnumerator<T>> GetExecutor(bool async)
    {
        throw new NotImplementedException();
    }
}

internal sealed class FirstStreamEnumerable<T>(StreamMergeContext context) : StreamEnumerable<T>(context)
{
    protected override IMergeExecutor<IStreamMergeEnumerator<T>> GetExecutor(bool async)
    {
        Context.Take = 1;
        return new ShardingEnumerableExecutor<T>(Context, async);
    }
}

internal sealed class SingleStreamEnumerable<T>(StreamMergeContext context) : StreamEnumerable<T>(context)
{
    protected override IMergeExecutor<IStreamMergeEnumerator<T>> GetExecutor(bool async)
    {
        Context.Take = 2;
        return new ShardingEnumerableExecutor<T>(Context, async);
    }
}

internal sealed class LastStreamEnumerable<T>(StreamMergeContext context) : StreamEnumerable<T>(context)
{
    protected override IMergeExecutor<IStreamMergeEnumerator<T>> GetExecutor(bool async)
    {
        var skip = Context.Skip;
        Context.ReverseSorting();
        Context.Skip = 0;
        Context.Take = skip.GetValueOrDefault() + 1;
        var newQueryable = (IQueryable<T>)Context.RewriteQueryable.RemoveVisitor(nameof(Queryable.Skip), nameof(Queryable.Take)).RemoveAnyOrderBy().WithSort(Context.Sorts).ReTake(Context.Take.Value);
        return new LastEnumerableExecutor<T>(Context, newQueryable, async);
    }
}

internal sealed class OrderStreamEnumerable<T>(StreamMergeContext context, PagedSequenceOptions? dataSource, PagedSequenceOptions? table, ICollection<RouteQueryResult<long>> results) : StreamEnumerable<T>(context)
{
    private readonly PagedSequenceOptions? _dataSource = dataSource;
    private readonly PagedSequenceOptions? _table = table;
    private readonly ICollection<RouteQueryResult<long>> _results = results;

    protected override IEnumerable<ISqlRouteUnit> GetRouteUnits()
    {
        var skip = Context.Skip.GetValueOrDefault();
        if (skip < 0)
        {
            throw new ShardingException("skip must ge 0");
        }

        var take = Context.Take;
        if (take.HasValue && take.Value <= 0)
        {
            throw new ShardingException("take must gt 0");
        }

        var sortResults = _results.Select(o => new
        {
            DataSource = o.DataSource ?? string.Empty,
            Tail = o.TableRouteResult!.ReplaceTables.First().Tail,
            RouteQueryResult = o
        });

        //分库是主要排序
        var dataSourceOpts = _dataSource;
        var tableOpts = _table;
        var sorts = new List<PropertySorting>();
        if (dataSourceOpts != null)
        {
            //if sharding data source 
            var direction = dataSourceOpts.Direction;
            //if sharding table
            var useThenBy = tableOpts != null;
            var tableComparer = tableOpts?.RouteComparer ?? Comparer<string>.Default;
            if (direction == SortDirection.Ascending)
            {
                sortResults = sortResults.OrderBy(o => o.DataSource, dataSourceOpts.RouteComparer).ThenByIf(o => o.Tail, useThenBy && tableOpts!.Direction == SortDirection.Ascending, tableComparer).ThenByDescendingIf(o => o.Tail, useThenBy && tableOpts!.Direction == SortDirection.Descending, tableComparer);
            }
            else
            {
                sortResults = sortResults.OrderByDescending(o => o.DataSource, dataSourceOpts.RouteComparer).ThenByDescendingIf(o => o.Tail, useThenBy, tableComparer);
            }

            sorts.Add(new PropertySorting(dataSourceOpts.PropertyName, dataSourceOpts.Direction, dataSourceOpts.OrderProperty.DeclaringType!));

            if (useThenBy)
            {
                sorts.Add(new PropertySorting(tableOpts!.PropertyName, tableOpts.Direction, tableOpts.OrderProperty.DeclaringType!));
            }
        }
        else
        {
            ArgumentNullException.ThrowIfNull(tableOpts);
            var direction = tableOpts.Direction;

            if (direction == SortDirection.Ascending)
            {
                sortResults = sortResults.OrderBy(o => o.Tail, tableOpts.RouteComparer);
            }
            else
            {
                sortResults = sortResults.OrderByDescending(o => o.Tail, tableOpts.RouteComparer);
            }

            sorts.Add(new PropertySorting(tableOpts.PropertyName, tableOpts.Direction, tableOpts.OrderProperty.DeclaringType!));
        }

        var sequenceResults = new SequencePagedList(sortResults.Select(o => o.RouteQueryResult)).WithSkip(skip).WithTake(take).ToList();

        Context.Sorts = [.. sorts];
        return sequenceResults.Select(sequenceResult => new SequenceRouteUnit(sequenceResult));
    }

    protected override IMergeExecutor<IStreamMergeEnumerator<T>> GetExecutor(bool async)
    {
        return new OrderEnumerableExecutor<T>(Context, async);
    }
}

internal sealed class ReverseStreamEnumerable<T>(StreamMergeContext context, long total) : StreamEnumerable<T>(context)
{
    private readonly long _total = total;

    protected override IMergeExecutor<IStreamMergeEnumerator<T>> GetExecutor(bool async)
    {
        var query = Context.OriginalQueryable.RemoveVisitor(nameof(Queryable.Skip), nameof(Queryable.Take)).RemoveAnyOrderBy() as IQueryable<T>
            ?? throw new ShardingException("Unable to rewrite reverse queryable.");

        var skip = Context.Skip.GetValueOrDefault();
        var take = Context.Take ?? _total - skip;
        if (take > int.MaxValue)
        {
            throw new ShardingException($"not support take more than {int.MaxValue}");
        }

        var realSkip = _total - take - skip;

        Context.Skip = (int)realSkip;
        Context.ReverseSorting();
        var reverse = query.Take((int)realSkip + (int)take).WithSort(Context.Sorts);
        return new ReverseEnumerableExecutor<T>(Context, reverse, async);
    }
}

internal sealed class SequenceStreamEnumerable<T>(StreamMergeContext context, PagedSequenceOptions? dataSource, PagedSequenceOptions? table, ICollection<RouteQueryResult<long>> results, SortDirection direction) : StreamEnumerable<T>(context)
{
    private readonly PagedSequenceOptions? _dataSource = dataSource;
    private readonly PagedSequenceOptions? _table = table;
    private readonly ICollection<RouteQueryResult<long>> _results = results;
    private readonly SortDirection _direction = direction;

    protected override IEnumerable<ISqlRouteUnit> GetRouteUnits()
    {
        var skip = Context.Skip.GetValueOrDefault();
        if (skip < 0)
        {
            throw new ShardingException("skip must ge 0");
        }

        var take = Context.Take;
        if (take.HasValue && take.Value <= 0)
        {
            throw new ShardingException("take must gt 0");
        }

        var sortResults = _results.Select(o => new
        {
            DataSource = o.DataSource ?? string.Empty,
            Tail = o.TableRouteResult!.ReplaceTables.First().Tail,
            RouteQueryResult = o
        });

        //分库是主要排序
        var dataSourceOpts = _dataSource;
        var tableOpts = _table;
        if (dataSourceOpts != null)
        {
            //if sharding table
            var useThenBy = tableOpts != null;
            var tableComparer = tableOpts?.RouteComparer ?? Comparer<string>.Default;
            if (_direction == SortDirection.Ascending)
            {
                sortResults = sortResults.OrderBy(o => o.DataSource, dataSourceOpts.RouteComparer).ThenByIf(o => o.Tail, useThenBy, tableComparer);
            }
            else
            {
                sortResults = sortResults.OrderByDescending(o => o.DataSource, dataSourceOpts.RouteComparer).ThenByDescendingIf(o => o.Tail, useThenBy, tableComparer);
            }
        }
        else
        {
            ArgumentNullException.ThrowIfNull(tableOpts);
            var direction = tableOpts.Direction;

            if (direction == SortDirection.Ascending)
            {
                sortResults = sortResults.OrderBy(o => o.Tail, tableOpts.RouteComparer);
            }
            else
            {
                sortResults = sortResults.OrderByDescending(o => o.Tail, tableOpts.RouteComparer);
            }
        }

        var sequenceResults = new SequencePagedList(sortResults.Select(o => o.RouteQueryResult)).WithSkip(skip).WithTake(take).ToList();
        return sequenceResults.Select(sequenceResult => new SequenceRouteUnit(sequenceResult));
    }

    protected override IMergeExecutor<IStreamMergeEnumerator<T>> GetExecutor(bool async)
    {
        return new SequenceEnumerableExecutor<T>(Context, async);
    }
}

using BeniceSoft.Core;
using System.Collections;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal sealed class StreamMergeEnumerable<T>(StreamMergeContext context) : IAsyncEnumerable<T>, IEnumerable<T>
{
    private readonly StreamMergeContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (!_context.TryPrepareExecute(() => new EmptyQueryEnumerator<T>(), out var emptyQueryEnumerator))
        {
            return new OwnedAsyncEnumerator<T>(emptyQueryEnumerator, _context);
        }

        var stream = new StreamMergeEnumerableFactory<T>(_context).GetStreamEnumerable();
        IAsyncEnumerator<T> enumerator = stream.GetAsyncEnumerator(cancellationToken);

        if (_context.UseTrack(typeof(T)))
        {
            enumerator = new QueryTrackerAsyncEnumerator<T>(_context.DbContext, enumerator);
        }

        // stream.Dispose → Context.Dispose；查询结束后必须回收 ParallelQuery DbContext
        return new OwnedAsyncEnumerator<T>(enumerator, stream);
    }

    public IEnumerator<T> GetEnumerator()
    {
        if (!_context.TryPrepareExecute(() => new EmptyQueryEnumerator<T>(), out var emptyQueryEnumerator))
        {
            return new OwnedEnumerator<T>(emptyQueryEnumerator, _context);
        }

        var stream = new StreamMergeEnumerableFactory<T>(_context).GetStreamEnumerable();
        IEnumerator<T> enumerator = stream.GetEnumerator();

        if (_context.UseTrack(typeof(T)))
        {
            enumerator = new QueryTrackerEnumerator<T>(_context.DbContext, enumerator);
        }

        return new OwnedEnumerator<T>(enumerator, stream);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// 枚举结束后释放所有者（StreamEnumerable / StreamMergeContext），避免 ParallelQuery DbContext 泄漏。
/// </summary>
internal sealed class OwnedAsyncEnumerator<T>(IAsyncEnumerator<T> inner, IDisposable owner) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;

    public ValueTask<bool> MoveNextAsync() => inner.MoveNextAsync();

    public async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync().ConfigureAwait(false);
        if (owner is IAsyncDisposable asyncOwner)
        {
            await asyncOwner.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            owner.Dispose();
        }
    }
}

internal sealed class OwnedEnumerator<T>(IEnumerator<T> inner, IDisposable owner) : IEnumerator<T>
{
    public T Current => inner.Current;

    object IEnumerator.Current => Current!;

    public bool MoveNext() => inner.MoveNext();

    public void Reset() => inner.Reset();

    public void Dispose()
    {
        inner.Dispose();
        owner.Dispose();
    }
}

internal sealed class StreamMergeEnumerableFactory<T>(StreamMergeContext context)
{
    private readonly IShardingPageManager _page = context.RuntimeContext.PageManager;
    private readonly IDataSourceRouteManager _dataSource = context.RuntimeContext.DataSourceRouteManager;
    private readonly ITableRouteManager _table = context.RuntimeContext.TableRouteManager;
    private readonly IEntityMetadataManager _entity = context.RuntimeContext.EntityMetadataManager;

    public IDataSourceRoute GetRoute(Type entityType)
    {
        return _dataSource.GetRoute(entityType);
    }

    public IStreamEnumerable<T> GetStreamEnumerable()
    {
        if (context.RouteNotMatch)
        {
            return new EmptyStreamEnumerable<T>(context);
        }

        if (context.UseMerge)
        {
            return new ShardingStreamEnumerable<T>(context);
        }

        var name = context.MergeContext.Name;
        switch (name)
        {
            case nameof(Enumerable.First):
            case nameof(Enumerable.FirstOrDefault):
                return new FirstStreamEnumerable<T>(context);
            case nameof(Enumerable.Single):
            case nameof(Enumerable.SingleOrDefault):
                return new SingleStreamEnumerable<T>(context);
            case nameof(Enumerable.Last):
            case nameof(Enumerable.LastOrDefault):
                return new LastStreamEnumerable<T>(context);
        }

        //未开启系统分表或者本次查询涉及多张分表
        if (context.PagedQuery && context.Single && _page.Current != null)
        {
            //获取虚拟表判断是否启用了分页配置
            var entityType = context.SingleType;
            if (entityType == null)
            {
                throw new ShardingException($"query not found sharding data source or sharding table entity");
            }

            if (context.Sorts.IsNull())
            {
                //自动添加属性顺序排序
                //除了判断属性名还要判断所属关系
                var mergeEngine = GetOrderStreamEnumerable(entityType);
                if (mergeEngine != null)
                {
                    return mergeEngine;
                }
            }
            else
            {
                var mergeEngine = GetPagedStreamEnumerable(entityType);

                if (mergeEngine != null)
                {
                    return mergeEngine;
                }
            }
        }

        return new ShardingStreamEnumerable<T>(context);
    }

    private OrderStreamEnumerable<T>? GetOrderStreamEnumerable(Type entityType)
    {
        var isSharding = _entity.IsShardingDataSource(entityType);
        var isShardingTable = _entity.IsShardingTable(entityType);
        PagedSequenceOptions? dataSource = null;
        PagedSequenceOptions? table = null;
        if (isSharding)
        {
            var route = GetRoute(entityType);
            if (route.EnablePaged)
            {
                dataSource = route.PagedMetadata!.Sequences.OrderByDescending(o => o.Order).FirstOrDefault(o => o.DefaultAppended && typeof(T).ContainsProperty(o.PropertyName));
            }
        }

        if (isShardingTable)
        {
            var tableRoute = _table.GetRoute(entityType);
            if (tableRoute.EnablePaged)
            {
                table = tableRoute.PagedMetadata!.Sequences.OrderByDescending(o => o.Order).FirstOrDefault(o => o.DefaultAppended && typeof(T).ContainsProperty(o.PropertyName));
            }
        }

        var useSequence = isSharding && (dataSource != null || isShardingTable && !context.IsCrossDataSource) || !isSharding && isShardingTable && table != null;

        if (useSequence)
        {
            return new OrderStreamEnumerable<T>(context, dataSource, table, _page.Current!.Results);
        }

        return null;
    }

    private IStreamEnumerable<T>? GetPagedStreamEnumerable(Type shardingEntityType)
    {

        var orderCount = context.Sorts.Length;
        var primaryOrder = context.Sorts[0];
        var isSharding = _entity.IsShardingDataSource(shardingEntityType);
        var isShardingTable = _entity.IsShardingTable(shardingEntityType);
        PagedSequenceOptions? dataSource = null;
        PagedSequenceOptions? table = null;
        IDataSourceRoute? dataSourceRoute = null;
        ITableRoute? tableRoute = null;
        var dataSourceUseReverse = true;
        var tableUseReverse = true;
        if (isSharding)
        {
            dataSourceRoute = GetRoute(shardingEntityType);
            if (dataSourceRoute.EnablePaged)
            {
                dataSource = orderCount == 1 ? GetPagedMatch(dataSourceRoute.PagedMetadata!.Sequences, primaryOrder) : GetPagedPrimaryMatch(dataSourceRoute.PagedMetadata!.Sequences, primaryOrder);
            }
        }

        if (isShardingTable)
        {
            tableRoute = _table.GetRoute(shardingEntityType);
            if (tableRoute.EnablePaged)
            {
                table = orderCount == 1 ? GetPagedMatch(tableRoute.PagedMetadata!.Sequences, primaryOrder) : GetPagedPrimaryMatch(tableRoute.PagedMetadata!.Sequences, primaryOrder);
            }
        }

        var useSequence = isSharding && (dataSource != null || isShardingTable && !context.IsCrossDataSource) || !isSharding && isShardingTable && table != null;
        if (useSequence)
        {
            return new SequenceStreamEnumerable<T>(context, dataSource, table, _page.Current!.Results, primaryOrder.Direction);
        }

        var total = _page.Current!.Results.Sum(o => o.Result);
        if (isSharding)
        {
            dataSourceUseReverse = dataSourceRoute!.EnablePaged && UseReverse(dataSourceRoute, total);
        }

        if (isShardingTable)
        {
            tableUseReverse = tableRoute!.EnablePaged && ReverseShardingPage(tableRoute, total);
        }

        //skip过大reserve skip
        if (dataSourceUseReverse && tableUseReverse)
        {
            return new ReverseStreamEnumerable<T>(context, total);
        }

        return null;
    }

    private bool UseReverse(IDataSourceRoute dataSourceRoute, long total)
    {
        var metadata = dataSourceRoute.PagedMetadata;
        if (metadata is null)
        {
            return false;
        }

        if (metadata.EnableReverse && context.Take.GetValueOrDefault() > 0)
        {
            if (metadata.UseReverse(context.Skip.GetValueOrDefault(), total))
            {
                return true;
            }
        }

        return false;
    }

    private bool ReverseShardingPage(ITableRoute tableRoute, long total)
    {
        var metadata = tableRoute.PagedMetadata;
        if (metadata is null)
        {
            return false;
        }

        if (metadata.EnableReverse && context.Take.GetValueOrDefault() > 0)
        {
            if (metadata.UseReverse(context.Skip.GetValueOrDefault(), total))
            {
                return true;
            }
        }

        return false;
    }

    private PagedSequenceOptions? GetPagedMatch(ISet<PagedSequenceOptions> options, PropertySorting sort)
    {
        return options.FirstOrDefault(o => PagedPrimaryMatch(o, sort));
    }

    private PagedSequenceOptions? GetPagedPrimaryMatch(ISet<PagedSequenceOptions> options, PropertySorting sort)
    {
        return options.Where(o => o.MatchMode.HasFlag(PagedMatchMode.PrimaryMatch)).FirstOrDefault(o => PagedPrimaryMatch(o, sort));
    }

    private bool PagedPrimaryMatch(PagedSequenceOptions options, PropertySorting sort)
    {
        if (sort.Expression != options.PropertyName)
        {
            return false;
        }

        if (options.MatchMode.HasFlag(PagedMatchMode.Owner))
        {
            return context.SingleType == options.OrderProperty.DeclaringType;
        }

        if (options.MatchMode.HasFlag(PagedMatchMode.Named))
        {
            return sort.Expression == options.PropertyName;
        }

        return false;
    }
}

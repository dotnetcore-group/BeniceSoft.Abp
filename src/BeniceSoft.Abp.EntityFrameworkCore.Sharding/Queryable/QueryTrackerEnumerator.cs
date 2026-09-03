using System.Collections;
using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal sealed class QueryTrackerEnumerator<T> : IEnumerator<T>
{
    private readonly IShardingDbContext _db;
    private readonly IEnumerator<T> _enumerator;
    private readonly IQueryTracker _queryTrack;

    public QueryTrackerEnumerator(IShardingDbContext db, IEnumerator<T> enumerator)
    {
        var shardingRuntimeContext = ((DbContext)db).GetRuntimeContext();
        _db = db;
        _enumerator = enumerator;
        _queryTrack = shardingRuntimeContext.QueryTracker;
    }

    public T Current
    {
        get
        {
            var current = _enumerator.Current;
            if (current != null)
            {
                var attachedEntity = _queryTrack.Track(current, _db);
                if (attachedEntity != null)
                {
                    return (T)attachedEntity;
                }
            }

            return current!;
        }
    }

    object? IEnumerator.Current => Current;

    public void Dispose()
    {
        _enumerator.Dispose();
    }

    public bool MoveNext()
    {
        return _enumerator.MoveNext();
    }

    public void Reset()
    {
        _enumerator.Reset();
    }
}

internal sealed class QueryTrackerAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IShardingDbContext _db;
    private readonly IAsyncEnumerator<T> _enumerator;
    private readonly IQueryTracker _queryTrack;

    public QueryTrackerAsyncEnumerator(IShardingDbContext db, IAsyncEnumerator<T> enumerator)
    {
        var shardingRuntimeContext = ((DbContext)db).GetRuntimeContext();
        _db = db;
        _enumerator = enumerator;
        _queryTrack = shardingRuntimeContext.QueryTracker;
    }

    public T Current
    {
        get
        {
            var current = _enumerator.Current;
            if (current != null)
            {
                var attachedEntity = _queryTrack.Track(current, _db);
                if (attachedEntity != null)
                {
                    return (T)attachedEntity;
                }
            }

            return current;
        }
    }

    public ValueTask DisposeAsync()
    {
        return _enumerator.DisposeAsync();
    }

    public ValueTask<bool> MoveNextAsync()
    {
        return _enumerator.MoveNextAsync();
    }
}

internal sealed class QueryTrackerEnumerable<T>(IShardingDbContext shardingDbContext, IEnumerable<T> enumerable) : IEnumerable<T>
{
    public IEnumerator<T> GetEnumerator()
    {
        return new QueryTrackerEnumerator<T>(shardingDbContext, enumerable.GetEnumerator());
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

internal sealed class QueryTrackerAsyncEnumerable<T>(IShardingDbContext shardingDbContext, IAsyncEnumerable<T> enumerable) : IAsyncEnumerable<T>
{
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new QueryTrackerAsyncEnumerator<T>(shardingDbContext, enumerable.GetAsyncEnumerator(cancellationToken));
    }
}

internal sealed class EmptyQueryEnumerator<T> : IAsyncEnumerator<T>, IEnumerator<T>
{
    public T Current { get; } = default!;

    object? IEnumerator.Current { get; } = null;

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public bool MoveNext()
    {
        return false;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        return ValueTask.FromResult(false);
    }

    public void Reset()
    {
    }
}

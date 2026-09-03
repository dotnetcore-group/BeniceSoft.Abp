using System.Collections;
using System.Data.Common;

namespace BeniceSoft.Abp.EntityFrameworkCore;

/// <summary>延迟列表查询：首次枚举 / ToListAsync 时触发所属 Batch 执行。</summary>
public class QueryFutureEnumerable<T> : BaseQueryFuture, IEnumerable<T>
{
    private IEnumerable<T>? _result;

    public QueryFutureEnumerable(QueryFutureBatch? ownerBatch, IQueryable? query)
    {
        OwnerBatch = ownerBatch;
        Query = query;
    }

    public IEnumerator<T> GetEnumerator()
    {
        if (!HasValue)
        {
            OwnerBatch!.ExecuteQueries();
        }

        return (_result ?? []).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!HasValue)
        {
            await OwnerBatch!.ExecuteQueriesAsync(cancellationToken);
        }

        if (_result == null)
        {
            return [];
        }

        using var enumerator = _result.GetEnumerator();
        var list = new List<T>();
        while (enumerator.MoveNext())
        {
            list.Add(enumerator.Current);
        }

        return list;
    }

    public async Task<T[]> ToArrayAsync(CancellationToken cancellationToken = default)
    {
        var list = await ToListAsync(cancellationToken);
        return [.. list];
    }

    public override void SetResult(DbDataReader reader)
    {
        if (reader.GetType().FullName?.Contains("Oracle") == true)
        {
            reader = new QueryFutureOracleDbReader(reader);
        }

        var enumerator = GetQueryEnumerator<T>(reader);
        using (enumerator)
        {
            SetResult(enumerator);
        }
    }

    public void SetResult(IEnumerator<T> enumerator)
    {
        var list = new List<T>();
        while (enumerator.MoveNext())
        {
            list.Add(enumerator.Current);
        }

        _result = list;
        HasValue = true;
    }

    public override void ExecuteInMemory()
    {
        HasValue = true;
        _result = [.. ((IQueryable<T>)Query!)];
    }

    public override void GetResultDirectly()
    {
        GetResultDirectly((IQueryable<T>)Query!);
    }

    public override Task GetResultDirectlyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GetResultDirectly((IQueryable<T>)Query!);
        return Task.CompletedTask;
    }

    internal void GetResultDirectly(IQueryable<T> query)
    {
        using var enumerator = query.GetEnumerator();
        SetResult(enumerator);
    }
}

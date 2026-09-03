using System.Data.Common;

namespace BeniceSoft.Abp.EntityFrameworkCore;

/// <summary>延迟标量查询（如 Count/First）：访问 Value / ValueAsync 时触发 Batch。</summary>
public class QueryFutureValue<TResult> : BaseQueryFuture
{
    private TResult? _result;

    public QueryFutureValue(QueryFutureBatch? ownerBatch, IQueryable? query)
    {
        OwnerBatch = ownerBatch;
        Query = query;
    }

    public TResult Value
    {
        get
        {
            if (!HasValue)
            {
                OwnerBatch!.ExecuteQueries();
            }

            return _result!;
        }
    }

    public async Task<TResult> ValueAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!HasValue)
        {
            await OwnerBatch!.ExecuteQueriesAsync(cancellationToken);
        }

        return _result!;
    }

    public override void SetResult(DbDataReader reader)
    {
        if (reader.GetType().FullName?.Contains("Oracle") == true)
        {
            reader = new QueryFutureOracleDbReader(reader);
        }

        var enumerator = GetQueryEnumerator<TResult>(reader);
        using (enumerator)
        {
            enumerator.MoveNext();
            _result = enumerator.Current;
        }

        HasValue = true;
    }

    public override void ExecuteInMemory()
    {
        var query = (IQueryable<TResult>)Query!;
        var value = query.Provider.Execute<object>(query.Expression);
        AssignResult(value);
        HasValue = true;
    }

    public override void GetResultDirectly()
    {
        var query = (IQueryable<TResult>)Query!;
        var value = query.Provider.Execute<object>(query.Expression);
        AssignResult(value);
        HasValue = true;
    }

    public override Task GetResultDirectlyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GetResultDirectly();
        return Task.CompletedTask;
    }

    internal void GetResultDirectly(IQueryable<TResult> query)
    {
        _result = query.Provider.Execute<TResult>(query.Expression);
        HasValue = true;
    }

    private void AssignResult(object? value)
    {
        if (value is TResult valueTResult)
        {
            _result = valueTResult;
        }
        else if (value == null)
        {
            _result = default;
        }
        else if (value is IEnumerable<TResult> valueIEnumerable)
        {
            using var enumerator = valueIEnumerable.GetEnumerator();
            enumerator.MoveNext();
            _result = enumerator.Current;
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported FutureValue result type: {value.GetType().FullName}");
        }
    }

    public static implicit operator TResult(QueryFutureValue<TResult>? futureValue)
        => futureValue == null ? default! : futureValue.Value;
}

using BeniceSoft.Core;
using System.Collections;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IMemoryStreamMergeAsyncEnumerator
{
    int ReallyCount { get; }
}

internal interface IMemoryStreamMergeAsyncEnumerator<T> : IStreamMergeEnumerator<T>, IMemoryStreamMergeAsyncEnumerator
{
}

internal class MemoryStreamMergeAsyncEnumerator<T> : IMemoryStreamMergeAsyncEnumerator<T>
{
    private readonly bool _async;
    private readonly IEnumerator<T> _enumerator;
    private bool _skip;
    private bool _hasElement;

    public MemoryStreamMergeAsyncEnumerator(IStreamMergeEnumerator<T> asyncSource, bool async)
    {
        if (_enumerator != null)
        {
            throw new InvalidOperationException(nameof(_enumerator));
        }

        _async = async;

        if (_async)
        {
            _enumerator = GetAllRowsAsync(asyncSource).GetResult();
        }
        else
        {
            _enumerator = GetAllRows(asyncSource);
        }

        // 空结果时 MoveNext=false，不可再读 Current（.NET List.Enumerator 会抛 Enumeration already finished）
        _hasElement = _enumerator.MoveNext();
        _skip = true;
    }

    public bool SkipFirst
    {
        get
        {
            if (_skip)
            {
                _skip = false;
                return true;
            }

            return false;
        }
    }

    public bool HasElement => _hasElement;

    public T ReallyCurrent => _hasElement ? _enumerator.Current : default!;

    public T Current => GetCurrent();

    public int ReallyCount { get; private set; }

    object? IEnumerator.Current => Current;

    private async Task<IEnumerator<T>> GetAllRowsAsync(IAsyncEnumerator<T> enumerator)
    {
        var list = new List<T>();
        while (await enumerator.MoveNextAsync())
        {
            list.Add(enumerator.Current);
            ReallyCount++;
        }

        return GetEnumerator(list);
    }

    private IEnumerator<T> GetAllRows(IEnumerator<T> enumerator)
    {
        var list = new List<T>();
        while (enumerator.MoveNext())
        {
            list.Add(enumerator.Current);
            ReallyCount++;
        }

        return GetEnumerator(list);
    }

    protected virtual IEnumerator<T> GetEnumerator(IList<T> list)
    {
        return list.GetEnumerator();
    }

    public void Dispose()
    {
        _enumerator.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public bool MoveNext()
    {
        if (_skip)
        {
            _skip = false;
            return _hasElement;
        }

        _hasElement = _enumerator.MoveNext();
        return _hasElement;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        if (_skip)
        {
            _skip = false;
            return new ValueTask<bool>(_hasElement);
        }

        _hasElement = _enumerator.MoveNext();
        return new ValueTask<bool>(_hasElement);
    }

    public void Reset()
    {
        _enumerator.Reset();
    }

    public T GetCurrent()
    {
        if (_skip || !_hasElement)
        {
            return default!;
        }

        return _enumerator.Current;
    }
}

internal sealed class MemoryGroupStreamMergeAsyncEnumerator<T>(StreamMergeContext context, IStreamMergeEnumerator<T> asyncSource, bool async) : MemoryStreamMergeAsyncEnumerator<T>(asyncSource, async)
{
    protected override IEnumerator<T> GetEnumerator(IList<T> list)
    {
        return list.AsQueryable().WithSort(context.GroupByContext.Sorts).GetEnumerator();
    }
}

internal sealed class MemoryReverseStreamMergeAsyncEnumerator<T>(IStreamMergeEnumerator<T> enumerator) : IMemoryStreamMergeAsyncEnumerator<T>
{
    private readonly IStreamMergeEnumerator<T> _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:尽可能使用具体类型以提高性能", Justification = "<挂起>")]
    private IEnumerator<T>? _reverse;
    private bool _first = true;

    public bool SkipFirst => throw new NotSupportedException();

    public bool HasElement => throw new NotSupportedException();

    public T ReallyCurrent => Current;

    public T Current => GetCurrent();

    public int ReallyCount { get; private set; }

    object? IEnumerator.Current => Current;

    public void Dispose()
    {
        _enumerator.Dispose();
        _reverse?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _enumerator.DisposeAsync();
        _reverse?.Dispose();
    }

    public T GetCurrent()
    {
        return _reverse == null ? default! : _reverse.Current;
    }

    public bool MoveNext()
    {
        if (_first)
        {
            var list = new LinkedList<T>();
            while (_enumerator.MoveNext())
            {
                list.AddFirst(_enumerator.GetCurrent()!);
            }

            _reverse = list.GetEnumerator();
            _first = false;
        }

        return _reverse!.MoveNext();
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        if (_first)
        {
            var list = new LinkedList<T>();
            while (await _enumerator.MoveNextAsync())
            {
                list.AddFirst(_enumerator.GetCurrent()!);
            }

            _reverse = list.GetEnumerator();
            _first = false;
        }

        return _reverse!.MoveNext();
    }

    public void Reset()
    {
        throw new NotImplementedException();
    }
}

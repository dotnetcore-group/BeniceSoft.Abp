using BeniceSoft.Core;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IStreamMergeEnumerator<T> : IAsyncEnumerator<T>, IEnumerator<T>
{
    bool SkipFirst { get; }

    bool HasElement { get; }

    [MaybeNull]
    T ReallyCurrent { get; }

    [return: MaybeNull]
    T GetCurrent();
}

internal sealed class StreamMergeEnumerator<T> : IStreamMergeEnumerator<T>
{
    private readonly IAsyncEnumerator<T>? _asyncSource;
    private readonly IEnumerator<T>? _syncSource;
    private bool _skip;
    private readonly bool _asyncEnumerator;
    private readonly bool _syncEnumerator;

    public StreamMergeEnumerator(IAsyncEnumerator<T> asyncSource)
    {
        ArgumentNullException.ThrowIfNull(asyncSource);

        _asyncSource = asyncSource;
        _asyncEnumerator = true;
        _skip = true;
    }

    public StreamMergeEnumerator(IEnumerator<T> syncSource)
    {
        ArgumentNullException.ThrowIfNull(syncSource);

        _syncSource = syncSource;
        _syncEnumerator = true;
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

    public async ValueTask DisposeAsync()
    {
        if (_asyncEnumerator)
        {
            await _asyncSource!.DisposeAsync();
        }
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        if (_skip)
        {
            _skip = false;
            return _asyncSource!.Current != null;
        }

        return await _asyncSource!.MoveNextAsync();
    }

    public void Dispose()
    {
        _syncSource?.Dispose();
    }

    public bool MoveNext()
    {
        if (_skip)
        {
            _skip = false;
            return _syncSource!.Current != null;
        }

        return _syncSource!.MoveNext();
    }

    public void Reset()
    {
        throw new NotImplementedException();
    }

    object? IEnumerator.Current => Current;

    T IEnumerator<T>.Current => Current!;

    T IAsyncEnumerator<T>.Current => Current!;

    [MaybeNull]
    public T Current => GetCurrent();

    [MaybeNull]
    public T ReallyCurrent => GetReallyCurrent();

    public bool HasElement
    {
        get
        {

            if (_asyncEnumerator)
            {
                return _asyncSource!.Current != null;
            }

            if (_syncEnumerator)
            {
                return _syncSource!.Current != null;
            }

            return false;

        }
    }

    [return: MaybeNull]
    public T GetCurrent()
    {
        // skip 首元素时仍返回当前缓冲值
        if (_asyncEnumerator)
        {
            return _asyncSource!.Current;
        }

        if (_syncEnumerator)
        {
            return _syncSource!.Current;
        }

        return default;
    }

    [return: MaybeNull]
    private T GetReallyCurrent()
    {
        if (_asyncEnumerator)
        {
            return _asyncSource!.Current;
        }

        if (_syncEnumerator)
        {
            return _syncSource!.Current;
        }

        return default;
    }
}

internal sealed class MostStreamMergeEnumerator<T>(IStreamMergeEnumerator<T> enumerator) : IStreamMergeEnumerator<T>
{
    private int _moveIndex = -1;

    public bool SkipFirst { get; }

    public bool HasElement => ReallyCurrent != null;

    [MaybeNull]
    public T ReallyCurrent { get; } = enumerator.ReallyCurrent!;

    [MaybeNull]
    public T Current { get; } = default!;

    object? IEnumerator.Current => Current;

    T IEnumerator<T>.Current => Current!;

    T IAsyncEnumerator<T>.Current => Current!;

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [return: MaybeNull]
    public T GetCurrent()
    {
        if (_moveIndex == 0)
        {
            return default;
        }

        return ReallyCurrent;
    }

    public bool MoveNext()
    {
        if (_moveIndex >= 1)
        {
            return false;
        }

        _moveIndex++;
        return HasElement;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        var ret = MoveNext();
        return ValueTask.FromResult(ret);
    }

    public void Reset()
    {
        _moveIndex = 0;
    }
}

internal sealed class AggregateStreamMergeEnumerator<T> : IStreamMergeEnumerator<T>
{
    private readonly StreamMergeContext _mergeContext;
    private readonly IEnumerable<IStreamMergeEnumerator<T>> _enumerators;
    private readonly BeniceSoft.Core.Strategy.PriorityQueue<IOrderByStreamMergeEnumerator<T>> _queue;
    private List<object?> _groups;

    public AggregateStreamMergeEnumerator(StreamMergeContext mergeContext, ICollection<IStreamMergeEnumerator<T>> enumerators)
    {
        _mergeContext = mergeContext;
        _enumerators = enumerators;

        _queue = new BeniceSoft.Core.Strategy.PriorityQueue<IOrderByStreamMergeEnumerator<T>>();
        foreach (var source in _enumerators)
        {
            var enumerator = new OrderByStreamMergeEnumerator<T>(_mergeContext, source);
            if (enumerator.HasElement)
            {
                _queue.Enqueue(enumerator);
            }
        }
        // 初始化分组键
        _groups = _queue.IsNull() ? [] : GetGroupValues(_queue.Peek());
    }

    private List<object?> GetGroupValues(IOrderByStreamMergeEnumerator<T> enumerator)
    {
        var first = enumerator.ReallyCurrent
                    ?? throw new ShardingInvalidOperationException("aggregate enumerator has no current element");
        return _mergeContext.SelectContext.Properties.FindAll(o => o is not SelectAggregateProperty).Select(o => first.GetPropertyType(o.Name).value).ToList();
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        if (_queue.IsNull())
        {
            return false;
        }

        var hasNext = await SetCurrentValueAsync();
        if (hasNext)
        {
            _groups = _queue.IsNull() ? [] : GetGroupValues(_queue.Peek());
        }

        return hasNext;
    }

    private bool EqualWithGroupValues()
    {
        var current = GetGroupValues(_queue.Peek());
        for (var i = 0; i < _groups.Count; i++)
        {
            if (!object.Equals(_groups[i], current[i]))
            {
                return false;
            }
        }

        return true;
    }

    private async ValueTask<bool> SetCurrentValueAsync()
    {
        Current = default!;
        var currentValues = new List<T>();
        while (EqualWithGroupValues())
        {
            var current = _queue.Peek().GetCurrent();
            currentValues.Add(current!);
            var first = _queue.Dequeue();

            if (await first.MoveNextAsync())
            {
                _queue.Enqueue(first);
            }

            if (_queue.IsNull())
            {
                break;
            }
        }

        MergeValue(currentValues);

        return true;
    }
    public bool MoveNext()
    {
        if (_queue.IsNull())
        {
            return false;
        }

        var hasNext = SetCurrentValue();
        if (hasNext)
        {
            _groups = _queue.IsNull() ? [] : GetGroupValues(_queue.Peek());
        }

        return hasNext;
    }
    private bool SetCurrentValue()
    {
        Current = default!;
        var currentValues = new List<T>();
        while (EqualWithGroupValues())
        {
            var current = _queue.Peek().GetCurrent();
            currentValues.Add(current!);
            var first = _queue.Dequeue();

            if (first.MoveNext())
            {
                _queue.Enqueue(first);
            }

            if (_queue.IsNull())
            {
                break;
            }
        }

        MergeValue(currentValues);

        return true;
    }

    private void MergeValue(List<T> aggregateValues)
    {
        if (aggregateValues.IsNull())
        {
            return;
        }

        // 分片结果可能含空流产生的 null，合并前剔除
        var values = aggregateValues.Where(o => o is not null).ToList();
        if (values.Count == 0)
        {
            return;
        }

        Current = CopyToSource(values[0]!);

        if (values.Count > 1)
        {
            var aggregates = _mergeContext.SelectContext.Properties.OfType<SelectAggregateProperty>().ToList();
            if (aggregates.IsNotNull())
            {
                var propertyValues = new LinkedList<(string name, object? value)>();
                foreach (var aggregate in aggregates)
                {
                    object? aggregateValue = null;
                    if (aggregate is SelectCountProperty or SelectSumProperty)
                    {
                        aggregateValue = values.AsQueryable().SumBy(aggregate.Property);
                    }
                    else if (aggregate is SelectMaxProperty)
                    {
                        aggregateValue = values.AsQueryable().MaxBy(aggregate.Property);
                    }
                    else if (aggregate is SelectMinProperty)
                    {
                        aggregateValue = values.AsQueryable().MinBy(aggregate.Property);
                    }
                    else if (aggregate is SelectAverageProperty selectAverageProperty)
                    {
                        if (selectAverageProperty.CountProperty != null)
                        {
                            aggregateValue = values.AsQueryable().AverageCount(selectAverageProperty.Property, selectAverageProperty.CountProperty, selectAverageProperty.Property.PropertyType);
                        }
                        else if (selectAverageProperty.SumProperty != null)
                        {
                            aggregateValue = values.AsQueryable().AverageSum(selectAverageProperty.Property, selectAverageProperty.SumProperty, selectAverageProperty.Property.PropertyType);
                        }
                        else
                        {
                            throw new ShardingInvalidOperationException($"method:{aggregate.MethodName} invalid operation ");
                        }
                    }
                    else
                    {
                        throw new ShardingInvalidOperationException($"method:{aggregate.MethodName} invalid operation ");
                    }

                    propertyValues.AddLast((aggregate.Name, aggregateValue));
                }

                foreach (var (name, value) in propertyValues)
                {
                    Current!.SetPropertyValue(name, value);
                }
            }
        }
    }

    private static TSource CopyToSource<TSource>(TSource source)
    {
        if (source is null)
        {
            return source;
        }

        var anonType = source.GetType();
        var allProperties = anonType.GetProperties();
        var allPropertyTypes = allProperties.Select(o => o.PropertyType).ToArray();
        if (IsAnonymousType(anonType))
        {
            // 匿名类型属性顺序与构造参数顺序不一定一致，必须按 ctor 参数拷贝
            var constructorInfo = anonType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault()
                ?? throw new ShardingInvalidOperationException($"anonymous type [{anonType}] has no constructor");

            var args = constructorInfo.GetParameters().Select(p =>
            {
                var property = anonType.GetProperty(p.Name!)
                    ?? throw new ShardingInvalidOperationException($"anonymous type [{anonType}] missing property [{p.Name}]");
                return property.GetValue(source);
            }).ToArray();

            return (TSource)constructorInfo.Invoke(args);
        }
        else
        {
            if (anonType.GetConstructors().Length == 1 &&
                anonType.GetConstructors()[0].GetParameters().Length == allPropertyTypes.Length)
            {
                var parameters = allProperties.Select(o => o.GetValue(source)).ToArray();
                return (TSource)(Activator.CreateInstance(anonType, parameters)
                       ?? throw new ShardingInvalidOperationException($"failed to create instance of [{anonType}]"));
            }
            else
            {
                var instance = (TSource)(Activator.CreateInstance(anonType)
                               ?? throw new ShardingInvalidOperationException($"failed to create instance of [{anonType}]"));
                foreach (var property in allProperties)
                {
                    var value = property.GetValue(source);
                    instance.SetPropertyValue(property.Name, value);
                }

                return instance;
            }
        }
    }

    private static bool IsAnonymousType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        // The only way to detect anonymous types right now.
        return Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false) && type.IsGenericType && type.Name.Contains("AnonymousType") && (type.Name.StartsWith("<>") || type.Name.StartsWith("VB$")) && type.Attributes.HasFlag(TypeAttributes.NotPublic);
    }

    public bool SkipFirst { get; } = true;

    public bool HasElement => ReallyCurrent != null;

    [MaybeNull]
    public T ReallyCurrent => _queue.IsNull() ? default : _queue.Peek().ReallyCurrent;

    [return: MaybeNull]
    public T GetCurrent()
    {
        return Current;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var enumerator in _enumerators)
        {
            await enumerator.DisposeAsync();
        }
    }

    public void Reset()
    {
        throw new NotImplementedException();
    }

    object? IEnumerator.Current => Current;

    T IEnumerator<T>.Current => Current!;

    T IAsyncEnumerator<T>.Current => Current!;

    [MaybeNull]
    public T Current { get; private set; }

    public void Dispose()
    {
        foreach (var enumerator in _enumerators)
        {
            enumerator.Dispose();
        }
    }
}

internal sealed class OrderStreamMergeEnumerator<T> : IStreamMergeEnumerator<T>
{

    private readonly StreamMergeContext _mergeContext;
    private readonly IEnumerable<IStreamMergeEnumerator<T>> _enumerators;
    private readonly BeniceSoft.Core.Strategy.PriorityQueue<IOrderByStreamMergeEnumerator<T>> _queue;
    private IStreamMergeEnumerator<T>? _current;
    private bool _skipFirst;

    public OrderStreamMergeEnumerator(StreamMergeContext mergeContext, IEnumerable<IStreamMergeEnumerator<T>> enumerators)
    {
        _mergeContext = mergeContext;
        _enumerators = enumerators;
        _queue = new BeniceSoft.Core.Strategy.PriorityQueue<IOrderByStreamMergeEnumerator<T>>();
        _skipFirst = true;
        SetOrderEnumerator();
    }

    private void SetOrderEnumerator()
    {
        foreach (var source in _enumerators)
        {
            var enumerator = new OrderByStreamMergeEnumerator<T>(_mergeContext, source);
            if (enumerator.HasElement)
            {
                _queue.Enqueue(enumerator);
            }
        }

        _current = _queue.IsNull() ? null : _queue.Peek();
        ConsumePeek(_current);
    }

    private static void ConsumePeek(IStreamMergeEnumerator<T>? enumerator)
    {
        // ????????????? SkipFirst????? MoveNext ?????
        if (enumerator is not null && enumerator.HasElement)
        {
            _ = enumerator.SkipFirst;
        }
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        if (_queue.IsNull())
        {
            return false;
        }

        if (_skipFirst)
        {
            _skipFirst = false;
            return true;
        }

        var first = _queue.Dequeue();

        if (await first.MoveNextAsync())
        {
            _queue.Enqueue(first);
        }

        if (_queue.IsNull())
        {
            _current = null;
            return false;
        }

        _current = _queue.Peek();
        ConsumePeek(_current);
        return true;
    }

    public bool MoveNext()
    {
        if (_queue.IsNull())
        {
            return false;
        }

        if (_skipFirst)
        {
            _skipFirst = false;
            return true;
        }

        var first = _queue.Dequeue();
        if (first.MoveNext())
        {
            _queue.Enqueue(first);
        }

        if (_queue.IsNull())
        {
            _current = null;
            return false;
        }

        _current = _queue.Peek();
        ConsumePeek(_current);
        return true;
    }

    public bool SkipFirst
    {
        get
        {
            if (_skipFirst)
            {
                _skipFirst = false;
                return true;
            }

            return false;
        }
    }

    public bool HasElement => _current != null && _current.HasElement;

    [MaybeNull]
    public T ReallyCurrent => _queue.IsNull() ? default : _queue.Peek().ReallyCurrent;

    [return: MaybeNull]
    public T GetCurrent()
    {
        return _current is null ? default : _current.GetCurrent();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var enumerator in _enumerators)
        {
            await enumerator.DisposeAsync();
        }
    }

    public void Reset()
    {
        throw new NotImplementedException();
    }

    object? IEnumerator.Current => Current;

    T IEnumerator<T>.Current => Current!;

    T IAsyncEnumerator<T>.Current => Current!;

    [MaybeNull]
    public T Current => _skipFirst || _current is null ? default : _current.GetCurrent();

    public void Dispose()
    {
        foreach (var enumerator in _enumerators)
        {
            enumerator.Dispose();
        }
    }
}

internal sealed class PagedStreamMergeEnumerator<T> : IStreamMergeEnumerator<T>
{
    private readonly StreamMergeContext _mergeContext;
    private readonly IStreamMergeEnumerator<T> _enumerator;
    private readonly int? _skip;
    private readonly int? _take;
    private int _realSkip;
    private int _realTake;

    public PagedStreamMergeEnumerator(StreamMergeContext mergeContext, ICollection<IStreamMergeEnumerator<T>> sources) : this(mergeContext, sources, mergeContext.Skip, mergeContext.Take)
    {
    }

    public PagedStreamMergeEnumerator(StreamMergeContext mergeContext, ICollection<IStreamMergeEnumerator<T>> sources, int? skip, int? take)
    {
        _mergeContext = mergeContext;
        _skip = skip;
        _take = take;
        if (_mergeContext.HasGroup)
        {
            _enumerator = new AggregateStreamMergeEnumerator<T>(_mergeContext, sources);
        }
        else
        {
            _enumerator = new OrderStreamMergeEnumerator<T>(_mergeContext, sources);
        }
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        //?????????????????take????????next
        while (_skip.GetValueOrDefault() > _realSkip)
        {
            var has = await _enumerator.MoveNextAsync();

            _realSkip++;
            if (!has)
            {
                return false;
            }
        }

        var next = await _enumerator.MoveNextAsync();

        if (next)
        {
            if (_take.HasValue)
            {
                _realTake++;
                if (_realTake > _take.Value)
                {
                    return false;
                }
            }
        }

        return next;
    }

    public bool MoveNext()
    {
        //?????????????????take????????next
        while (_skip.GetValueOrDefault() > _realSkip)
        {
            var has = _enumerator.MoveNext();
            _realSkip++;
            if (!has)
            {
                return false;
            }
        }

        var next = _enumerator.MoveNext();

        if (next)
        {
            if (_take.HasValue)
            {
                _realTake++;
                if (_realTake > _take.Value)
                {
                    return false;
                }
            }
        }

        return next;
    }

    public void Reset()
    {
        throw new NotImplementedException();
    }

    object? IEnumerator.Current => Current;

    T IEnumerator<T>.Current => Current!;

    T IAsyncEnumerator<T>.Current => Current!;

    [MaybeNull]
    public T Current => _enumerator.GetCurrent();

    public bool SkipFirst => _enumerator.SkipFirst;

    public bool HasElement => _enumerator.HasElement;

    [MaybeNull]
    public T ReallyCurrent => _enumerator.ReallyCurrent;

    [return: MaybeNull]
    public T GetCurrent()
    {
        return _enumerator.GetCurrent();
    }
    public void Dispose()
    {
        _enumerator.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _enumerator.DisposeAsync();
    }
}

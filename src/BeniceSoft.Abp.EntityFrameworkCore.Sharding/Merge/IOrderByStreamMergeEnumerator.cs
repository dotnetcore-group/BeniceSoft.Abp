using BeniceSoft.Core;
using System.Collections;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IOrderByStreamMergeEnumerator<T> : IStreamMergeEnumerator<T>, IComparable<IOrderByStreamMergeEnumerator<T>>, IComparable
{
    IReadOnlyList<IComparable> GetComparables();
}

internal sealed class OrderByStreamMergeEnumerator<T> : IOrderByStreamMergeEnumerator<T>
{
    /// <summary>
    /// 合并数据上下文
    /// </summary>
    private readonly StreamMergeContext _mergeContext;
    private readonly IStreamMergeEnumerator<T> _enumerator;

    private List<IComparable>? _orderValues;

    public OrderByStreamMergeEnumerator(StreamMergeContext mergeContext, IStreamMergeEnumerator<T> enumerator)
    {
        _mergeContext = mergeContext;
        _enumerator = enumerator;
        SetOrderValues();
    }

    private void SetOrderValues()
    {
        _orderValues = HasElement ? GetCurrentOrderValues() : [];
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        var has = await _enumerator.MoveNextAsync();
        SetOrderValues();
        return has;
    }

    public bool MoveNext()
    {
        var has = _enumerator.MoveNext();
        SetOrderValues();
        return has;
    }

    public void Reset()
    {
        throw new NotSupportedException();
    }

    object? IEnumerator.Current => Current;

    public T Current => GetCurrent();

    public void Dispose()
    {
        _enumerator.Dispose();
    }

    public bool SkipFirst => _enumerator.SkipFirst;

    public bool HasElement => _enumerator.HasElement;

    public T ReallyCurrent => _enumerator.ReallyCurrent!;

    public T GetCurrent()
    {
        return _enumerator.GetCurrent()!;
    }

    private List<IComparable> GetCurrentOrderValues()
    {
        if (_mergeContext.Sorts.IsNull() || _enumerator.ReallyCurrent is null)
        {
            return [];
        }

        var list = new List<IComparable>(_mergeContext.Sorts.Length);
        foreach (var order in _mergeContext.Sorts)
        {
            var (propertyType, value) = _enumerator.ReallyCurrent.GetPropertyType(order.Expression);
            if (value is IComparable comparable)
            {
                list.Add(comparable);
            }
            else if (typeof(IComparable).IsAssignableFrom(propertyType))
            {
                list.Add((IComparable)value!);
            }
            else
            {
                throw new NotSupportedException($"order by value [{order}] must  implements IComparable");
            }
        }

        return list;
    }

    public int CompareTo(IOrderByStreamMergeEnumerator<T>? other)
    {
        if (other == null)
        {
            return 0;
        }

        var i = 0;
        foreach (var order in _mergeContext.Sorts)
        {
            var result = _mergeContext.Comparer.Compare(_orderValues![i], other.GetComparables()[i], order.Direction);
            if (result != 0)
            {
                return result;
            }

            i++;
        }

        return 0;
    }

    public IReadOnlyList<IComparable> GetComparables()
    {
        return _orderValues ?? [];
    }

    public ValueTask DisposeAsync()
    {
        return _enumerator.DisposeAsync();
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as IOrderByStreamMergeEnumerator<T>);
    }
}

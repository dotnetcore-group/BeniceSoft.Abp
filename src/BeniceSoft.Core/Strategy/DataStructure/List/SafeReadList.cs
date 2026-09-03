using System.Collections;
using System.Collections.Immutable;

namespace BeniceSoft.Core.Strategy;

/// <summary>线程安全只读追加列表。</summary>
public class SafeReadList<T>(IEnumerable<T> list) : IReadOnlyList<T>
{
    private readonly Lock _locker = new();
    private ImmutableList<T> _list = ImmutableList.CreateRange(list);
    private List<T> _copyList = [];

    public SafeReadList() : this([])
    {
    }

    public int Count => _list.Count;

    public IReadOnlyList<T> CopyList
    {
        get
        {
            if (_copyList.Count != _list.Count)
            {
                lock (_locker)
                {
                    if (_copyList.Count != _list.Count)
                    {
                        _copyList = [.. _list];
                    }
                }
            }

            return _copyList;
        }
    }

    public T this[int index] => _list[index];

    public void Append(T value)
    {
        ImmutableList<T> original;
        ImmutableList<T> afterChange;
        do
        {
            original = _list;
            afterChange = _list.Add(value);
        } while (Interlocked.CompareExchange(ref _list, afterChange, original) != original);
    }

    public bool Contains(T value) => _list.Contains(value);

    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

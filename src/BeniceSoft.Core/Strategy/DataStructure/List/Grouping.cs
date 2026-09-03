using System.Collections;

namespace BeniceSoft.Core.Strategy;

public class Grouping<TKey, TElement> : IGrouping<TKey, TElement>, IList<TElement>
{
    private readonly IList<TElement> _elements = [];

    public TKey Key { get; }

    public int Count => _elements.Count;

    public bool IsReadOnly => _elements.IsReadOnly;

    public TElement this[int index]
    {
        get => _elements[index];

        set => _elements[index] = value;
    }

    public Grouping(TKey key)
    {
        Key = key;
    }

    public Grouping(TKey key, IList<TElement> elements)
    {
        Key = key;
        _elements = elements;
    }

    public IEnumerator<TElement> GetEnumerator()
    {
        return _elements.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int IndexOf(TElement item)
    {
        return _elements.IndexOf(item);
    }

    public void Insert(int index, TElement item)
    {
        _elements.Insert(index, item);
    }

    public void RemoveAt(int index)
    {
        _elements.RemoveAt(index);
    }

    public void Add(TElement item)
    {
        _elements.Add(item);
    }

    public void Clear()
    {
        _elements.Clear();
    }

    public bool Contains(TElement item)
    {
        return _elements.Contains(item);
    }

    public void CopyTo(TElement[] array, int arrayIndex)
    {
        _elements.CopyTo(array, arrayIndex);
    }

    public bool Remove(TElement item)
    {
        return _elements.Remove(item);
    }
}

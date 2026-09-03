using System.Collections;

namespace BeniceSoft.Core.Strategy;

/// <summary>优先队列。</summary>
public class PriorityQueue<T>(ICollection<T>? collection = null, SortDirection direction = SortDirection.Ascending, IComparer<T>? comparer = null)
    : IEnumerable<T>
    where T : IComparable
{
    private readonly BHeap<T> _heap = new(collection, direction, comparer);

    public int Count => _heap.Count;

    public void Enqueue(T item) => _heap.Add(item);

    public T Dequeue() => _heap.Extract();

    public T Peek() => _heap.Peek();

    public IEnumerator<T> GetEnumerator() => _heap.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

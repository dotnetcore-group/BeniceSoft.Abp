using System.Collections;

namespace BeniceSoft.Core.Strategy;

/// <summary>二叉堆。</summary>
public class BHeap<T> : IEnumerable<T>
    where T : IComparable
{
    private readonly CustomComparer<T> _comparer;
    private T[] _heapArray = null!;

    public int Count { get; private set; }

    public BHeap(ICollection<T>? collection = null, SortDirection direction = SortDirection.Ascending, IComparer<T>? comparer = null)
    {
        _comparer = new CustomComparer<T>(direction, comparer ?? Comparer<T>.Default);
        if (collection.IsNotNull())
        {
            Initial([.. collection]);
            Count = collection.Count;
        }
        else
        {
            _heapArray = new T[2];
        }
    }

    public void Add(T newItem)
    {
        if (Count == _heapArray.Length)
        {
            DoubleArray();
        }

        _heapArray[Count] = newItem;

        for (var i = Count; i > 0; i = (i - 1) / 2)
        {
            if (_comparer.Compare(_heapArray[i], _heapArray[(i - 1) / 2]) < 0)
            {
                _heapArray.Swap((i - 1) / 2, i);
            }
            else
            {
                break;
            }
        }

        Count++;
    }

    public T Extract()
    {
        if (Count == 0)
        {
            throw new InvalidDataException("empty heap");
        }

        var minMax = _heapArray[0];
        Remove(0);
        return minMax;
    }

    public T Peek()
    {
        if (Count == 0)
        {
            throw new InvalidDataException("empty heap");
        }

        return _heapArray[0];
    }

    public bool Remove(T value)
    {
        var index = FindIndex(value);
        if (index != -1)
        {
            Remove(index);
            return true;
        }

        return false;
    }

    public bool Contains(T value) => FindIndex(value) != -1;

    private void Initial(IList<T> list)
    {
        var i = (list.Count - 1) / 2;
        while (i >= 0)
        {
            Recursive(list, i);
            i--;
        }

        _heapArray = [.. list];
    }

    private void Recursive(IList<T> list, int index)
    {
        while (true)
        {
            var i = index;
            var left = 2 * i + 1;
            var right = 2 * i + 2;

            var minMax = left < list.Count && right < list.Count
                ? _comparer.Compare(list[left], list[right]) < 0 ? left : right
                : left < list.Count ? left : right < list.Count ? right : -1;

            if (minMax != -1 && _comparer.Compare(list[minMax], list[i]) < 0)
            {
                list.Swap(minMax, i);
                index = minMax;
                continue;
            }

            break;
        }
    }

    private void Remove(int parentIndex)
    {
        _heapArray[parentIndex] = _heapArray[Count - 1];
        Count--;

        while (true)
        {
            var leftIndex = 2 * parentIndex + 1;
            var rightIndex = 2 * parentIndex + 2;
            var parent = _heapArray[parentIndex];

            if (leftIndex < Count && rightIndex < Count)
            {
                var leftChild = _heapArray[leftIndex];
                var rightChild = _heapArray[rightIndex];
                var leftIsMinMax = _comparer.Compare(leftChild, rightChild) < 0;
                var minMaxChildIndex = leftIsMinMax ? leftIndex : rightIndex;

                if (_comparer.Compare(_heapArray[minMaxChildIndex], parent) < 0)
                {
                    _heapArray.Swap(parentIndex, minMaxChildIndex);
                    parentIndex = leftIsMinMax ? leftIndex : rightIndex;
                }
                else
                {
                    break;
                }
            }
            else if (leftIndex < Count)
            {
                if (_comparer.Compare(_heapArray[leftIndex], parent) < 0)
                {
                    _heapArray.Swap(parentIndex, leftIndex);
                    parentIndex = leftIndex;
                }
                else
                {
                    break;
                }
            }
            else if (rightIndex < Count)
            {
                if (_comparer.Compare(_heapArray[rightIndex], parent) < 0)
                {
                    _heapArray.Swap(parentIndex, rightIndex);
                    parentIndex = rightIndex;
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }

        if (_heapArray.Length / 2 == Count && _heapArray.Length > 2)
        {
            HalfArray();
        }
    }

    private int FindIndex(T value)
    {
        foreach (var i in Count)
        {
            if (_heapArray[i]!.Equals(value))
            {
                return i;
            }
        }

        return -1;
    }

    private void HalfArray()
    {
        var smallerArray = new T[_heapArray.Length / 2];
        foreach (var i in Count)
        {
            smallerArray[i] = _heapArray[i];
        }

        _heapArray = smallerArray;
    }

    private void DoubleArray()
    {
        var biggerArray = new T[_heapArray.Length * 2];
        foreach (var i in Count)
        {
            biggerArray[i] = _heapArray[i];
        }

        _heapArray = biggerArray;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IEnumerator<T> GetEnumerator() => _heapArray.Take(Count).GetEnumerator();
}

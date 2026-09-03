using System.Runtime.CompilerServices;

namespace BeniceSoft.Core;

internal sealed class DeepCloneState
{
    private MiniDictionary? _loops;
    private readonly object?[] _baseFromTo = new object?[6];
    private int _idx;

    public object? GetKnownRef(object from)
    {
        var baseFromTo = _baseFromTo;
        if (ReferenceEquals(from, baseFromTo[0]))
        {
            return baseFromTo[3];
        }

        if (ReferenceEquals(from, baseFromTo[1]))
        {
            return baseFromTo[4];
        }

        if (ReferenceEquals(from, baseFromTo[2]))
        {
            return baseFromTo[5];
        }

        return _loops?.FindEntry(from);
    }

    public void AddKnownRef(object from, object to)
    {
        if (_idx < 3)
        {
            _baseFromTo[_idx] = from;
            _baseFromTo[_idx + 3] = to;
            _idx++;
            return;
        }

        _loops ??= new MiniDictionary();
        _loops.Insert(from, to);
    }

    private sealed class MiniDictionary
    {
        private struct Entry
        {
            public int HashCode;
            public int Next;
            public object? Key;
            public object? Value;
        }

        private int[]? _buckets;
        private Entry[]? _entries;
        private int _count;

        public MiniDictionary(int capacity = 5)
        {
            if (capacity > 0)
            {
                Initialize(capacity);
            }
        }

        public object? FindEntry(object key)
        {
            if (_buckets is null || _entries is null)
            {
                return null;
            }

            var hashCode = RuntimeHelpers.GetHashCode(key) & 0x7FFFFFFF;
            var entries = _entries;
            for (var i = _buckets[hashCode % _buckets.Length]; i >= 0; i = entries[i].Next)
            {
                if (entries[i].HashCode == hashCode && ReferenceEquals(entries[i].Key, key))
                {
                    return entries[i].Value;
                }
            }

            return null;
        }

        private static readonly int[] Primes =
        [
            3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521,
            631, 761, 919, 1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419,
            10103, 12143, 14591, 17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431,
            90523, 108631, 130363, 156437, 187751, 225307, 270371, 324449, 389357, 467237, 560689,
            672827, 807403, 968897, 1162687, 1395263, 1674319, 2009191, 2411033, 2893249, 3471899,
            4166287, 4999559, 5999471, 7199369
        ];

        private static int GetPrime(int min)
        {
            foreach (var prime in Primes)
            {
                if (prime >= min)
                {
                    return prime;
                }
            }

            for (var i = min | 1; i < int.MaxValue; i += 2)
            {
                if (IsPrime(i) && (i - 1) % 101 != 0)
                {
                    return i;
                }
            }

            return min;
        }

        private static bool IsPrime(int candidate)
        {
            if ((candidate & 1) == 0)
            {
                return candidate == 2;
            }

            var limit = (int)Math.Sqrt(candidate);
            for (var divisor = 3; divisor <= limit; divisor += 2)
            {
                if (candidate % divisor == 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static int ExpandPrime(int oldSize)
        {
            var newSize = 2 * oldSize;
            if ((uint)newSize > 0x7FEFFFFD && 0x7FEFFFFD > oldSize)
            {
                return 0x7FEFFFFD;
            }

            return GetPrime(newSize);
        }

        private void Initialize(int size)
        {
            _buckets = new int[size];
            Array.Fill(_buckets, -1);
            _entries = new Entry[size];
        }

        public void Insert(object key, object value)
        {
            if (_buckets is null || _entries is null)
            {
                Initialize(0);
            }

            var buckets = _buckets!;
            var entries = _entries!;
            var hashCode = RuntimeHelpers.GetHashCode(key) & 0x7FFFFFFF;
            var targetBucket = hashCode % buckets.Length;

            if (_count == entries.Length)
            {
                Resize();
                buckets = _buckets!;
                entries = _entries!;
                targetBucket = hashCode % buckets.Length;
            }

            var index = _count++;
            entries[index].HashCode = hashCode;
            entries[index].Next = buckets[targetBucket];
            entries[index].Key = key;
            entries[index].Value = value;
            buckets[targetBucket] = index;
        }

        private void Resize()
            => Resize(ExpandPrime(_count));

        private void Resize(int newSize)
        {
            var newBuckets = new int[newSize];
            Array.Fill(newBuckets, -1);

            var newEntries = new Entry[newSize];
            Array.Copy(_entries!, 0, newEntries, 0, _count);

            for (var i = 0; i < _count; i++)
            {
                if (newEntries[i].HashCode >= 0)
                {
                    var bucket = newEntries[i].HashCode % newSize;
                    newEntries[i].Next = newBuckets[bucket];
                    newBuckets[bucket] = i;
                }
            }

            _buckets = newBuckets;
            _entries = newEntries;
        }
    }
}

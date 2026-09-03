namespace BeniceSoft.Core.Strategy;

public enum SortDirection
{
    Ascending = 0,
    Descending = 1
}

internal sealed class CustomComparer<T> : IComparer<T>
    where T : IComparable
{
    private readonly bool _isMax;
    private readonly IComparer<T> _comparer;

    internal CustomComparer(SortDirection sortDirection, IComparer<T> comparer)
    {
        _isMax = sortDirection == SortDirection.Descending;
        _comparer = comparer;
    }

    public int Compare(T? x, T? y)
    {
        return !_isMax ? _comparer.Compare(x, y) : _comparer.Compare(y, x);
    }
}

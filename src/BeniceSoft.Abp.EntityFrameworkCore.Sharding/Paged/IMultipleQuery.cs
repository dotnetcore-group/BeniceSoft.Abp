using System.Collections;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IMultipleQuery
{
    /// <summary>
    /// 是否继续执行多次查询
    /// </summary>
    /// <param name="total">总total</param>
    /// <param name="skip">还需跳过数目</param>
    /// <param name="real">执行的sql具体条数(路由条数)</param>
    /// <param name="times">已经执行了多少次了</param>
    /// <returns></returns>
    bool Continue(long total, int skip, int real, int times);
}

internal sealed class SimpleMultipleQuery : IMultipleQuery
{
    /// <summary>
    /// 如果需要跳过得条数大于5000并且已经执行次数小于路有数最大5次的情况下继续执行多次查询
    /// </summary>
    /// <param name="total"></param>
    /// <param name="skip"></param>
    /// <param name="real"></param>
    /// <param name="times"></param>
    /// <returns></returns>
    public bool Continue(long total, int skip, int real, int times)
    {
        if (skip > 5000)
        {
            if (times <= 5 && times <= real)
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class MultipleEnumerator<T> : IEnumerator<T>
{
    private readonly List<IEnumerator<T>> _enumerators;
    private int _index;
    private readonly int _enumeratorsCount;

    public MultipleEnumerator(IEnumerable<IEnumerator<T>> enumerators)
    {
        _enumerators = enumerators.ToList();
        _enumeratorsCount = _enumerators.Count;
        _index = 0;
    }
    public bool MoveNext()
    {
        if (_enumeratorsCount == 0)
        {
            return false;
        }

        if (_index >= _enumeratorsCount)
        {
            return false;
        }

        while (_index < _enumeratorsCount)
        {
            var moveNext = _enumerators[_index].MoveNext();
            if (moveNext)
            {
                return true;
            }

            _index++;
        }

        return false;
    }

    public void Reset()
    {
        throw new NotImplementedException();
    }

    public T Current => _enumerators[_index].Current;

    object? IEnumerator.Current => Current;

    public void Dispose()
    {
        _enumerators.Clear();
        GC.SuppressFinalize(this);
    }
}

using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace BeniceSoft.Core;

public static class ArrayUtils
{
    #region Paged
    /// <summary>
    /// data paging
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="aim"></param>
    /// <param name="pageIndex"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    public static IQueryable<T>? Paged<T>(this IQueryable<T>? aim, int pageIndex, int pageSize)
    {
        if (aim == null)
        {
            return aim;
        }

        return aim.Skip(pageIndex * pageSize).Take(pageSize);
    }

    /// <summary>
    /// data paging
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="aim"></param>
    /// <param name="pageIndex"></param>
    /// <param name="pageSize"></param>
    /// <param name="totalCount"></param>
    /// <returns></returns>
    public static IQueryable<T>? Paged<T>(this IQueryable<T>? aim, int pageIndex, int pageSize, out int totalCount)
    {
        if (aim == null)
        {
            totalCount = 0;
            return aim;
        }

        totalCount = aim.Count();
        return aim.Skip(pageIndex * pageSize).Take(pageSize);
    }

    /// <summary>
    /// data paging
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="aim"></param>
    /// <param name="pageIndex"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    public static IEnumerable<T>? Paged<T>(this IEnumerable<T>? aim, int pageIndex, int pageSize)
    {
        if (aim.IsNull())
        {
            return aim;
        }

        return aim!.Skip(pageIndex * pageSize).Take(pageSize);
    }

    /// <summary>
    /// data paging
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="aim"></param>
    /// <param name="pageIndex"></param>
    /// <param name="pageSize"></param>
    /// <param name="totalCount"></param>
    /// <returns></returns>
    public static IEnumerable<T>? Paged<T>(this IEnumerable<T>? aim, int pageIndex, int pageSize, out int totalCount)
    {
        if (aim.IsNull())
        {
            totalCount = 0;
            return default;
        }

        totalCount = aim!.Count();
        return aim!.Skip(pageIndex * pageSize).Take(pageSize);
    }

    /// <summary>
    /// 连接，只能用于内存数据操作 IQuery禁止使用
    /// </summary>
    /// <typeparam name="TOuter"></typeparam>
    /// <typeparam name="TInner"></typeparam>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="outer"></param>
    /// <param name="inner"></param>
    /// <param name="match"></param>
    /// <param name="resultSelector"></param>
    /// <returns></returns>
    public static IEnumerable<TResult> Join<TOuter, TInner, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TInner, bool> match, Func<TOuter, TInner, TResult> resultSelector)
    {
        var list = from o in outer
                   from n in inner.Where(t => match(o, t))
                   select new { Outer = o, Inner = n };
        return list.Select(t => resultSelector(t.Outer, t.Inner));
    }

    /// <summary>
    /// 左连接，只能用于内存数据操作 IQuery禁止使用
    /// </summary>
    /// <typeparam name="TOuter"></typeparam>
    /// <typeparam name="TInner"></typeparam>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="outer"></param>
    /// <param name="inner"></param>
    /// <param name="match"></param>
    /// <param name="resultSelector"></param>
    /// <returns></returns>
    public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TInner, bool> match, Func<TOuter, TInner, TResult> resultSelector)
    {
        var list = from o in outer
                   from n in inner.Where(t => match(o, t)).DefaultIfEmpty()
                   select new { Outer = o, Inner = n };
        return list.Select(t => resultSelector(t.Outer, t.Inner));
    }

    /// <summary>
    /// 左连接(此函数存在缺陷)
    /// 为了resultSelector的便利性，生成的SQL将会把两个表的字段全部查出来
    /// 避免此缺陷，需要outer和inner 都写Select所需的字段
    /// </summary>
    /// <typeparam name="TOuter"></typeparam>
    /// <typeparam name="TInner"></typeparam>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="outer"></param>
    /// <param name="inner"></param>
    /// <param name="outerKeySelector"></param>
    /// <param name="innerKeySelector"></param>
    /// <param name="resultSelector"></param>
    /// <returns></returns>
    public static IQueryable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(this IQueryable<TOuter> outer, IQueryable<TInner> inner, Expression<Func<TOuter, TKey>> outerKeySelector, Expression<Func<TInner, TKey>> innerKeySelector, Func<TOuter, TInner?, TResult> resultSelector)
    {
        var result = outer.GroupJoin(inner, outerKeySelector, innerKeySelector, (a, b) => new
        {
            Outer = a,
            Inner = b
        }).SelectMany(b => b.Inner.DefaultIfEmpty(), (a, b) => new { a.Outer, Inner = b }).Select(t => resultSelector(t.Outer, t.Inner));
        return result;
    }

    public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner?, TResult> resultSelector)
    {
        var result = outer.GroupJoin(inner, outerKeySelector, innerKeySelector, (a, b) =>
        {
            return new
            {
                Outer = a,
                Inner = b
            };
        }).SelectMany(b => b.Inner.DefaultIfEmpty(), (a, b) => resultSelector(a.Outer, b));
        return result;
    }

    public static IQueryable<T>? WhereIf<T>(this IQueryable<T>? aim, bool condition, Expression<Func<T, bool>> truePredicate, Expression<Func<T, bool>>? falsePredicate = null)
    {
        if (aim == null)
        {
            return aim;
        }

        if (condition && truePredicate != null)
        {
            return aim.Where(truePredicate);
        }

        if (!condition && falsePredicate != null)
        {
            return aim.Where(falsePredicate);
        }

        return aim;
    }

    public static IQueryable<T>? WhereIf<T>(this IQueryable<T>? aim, bool condition, Expression<Func<T, int, bool>> truePredicate, Expression<Func<T, int, bool>>? falsePredicate = null)
    {
        if (aim == null)
        {
            return aim;
        }

        if (condition && truePredicate != null)
        {
            return aim.Where(truePredicate);
        }

        if (!condition && falsePredicate != null)
        {
            return aim.Where(falsePredicate);
        }

        return aim;
    }

    public static IQueryable<T>? WhereSafe<T>(this IQueryable<T>? aim, Expression<Func<T, int, bool>>? predicate)
    {
        if (predicate == null || aim == null)
        {
            return aim;
        }

        return aim.Where(predicate);
    }

    public static IQueryable<T>? WhereSafe<T>(this IQueryable<T>? aim, Expression<Func<T, bool>>? predicate)
    {
        if (predicate == null || aim == null)
        {
            return aim;
        }

        return aim.Where(predicate);
    }

    public static IEnumerable<T>? WhereIf<T>(this IEnumerable<T>? aim, bool condition, Func<T, bool> truePredicate, Func<T, bool>? falsePredicate = null)
    {
        if (aim == null)
        {
            return aim;
        }

        if (condition && truePredicate != null)
        {
            return aim.Where(truePredicate);
        }

        if (!condition && falsePredicate != null)
        {
            return aim.Where(falsePredicate);
        }

        return aim;
    }

    public static IEnumerable<T>? WhereIf<T>(this IEnumerable<T>? aim, bool condition, Func<T, int, bool> truePredicate, Func<T, int, bool>? falsePredicate = null)
    {
        if (aim == null)
        {
            return aim;
        }

        if (condition && truePredicate != null)
        {
            return aim.Where(truePredicate);
        }

        if (!condition && falsePredicate != null)
        {
            return aim.Where(falsePredicate);
        }

        return aim;
    }

    public static IEnumerable<T>? WhereSafe<T>(this IEnumerable<T>? aim, Func<T, bool>? predicate)
    {
        if (predicate == null || aim == null)
        {
            return aim;
        }

        return aim.Where(predicate);
    }

    public static IEnumerable<T>? WhereSafe<T>(this IEnumerable<T>? aim, Func<T, int, bool>? predicate)
    {
        if (predicate == null || aim == null)
        {
            return aim;
        }

        return aim.Where(predicate);
    }
    #endregion

    #region Operate
    /// <summary>
    /// the specified value in the array is replaced
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="aim"></param>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>
    public static void Replace<T>(this IList<T> aim, T oldValue, T newValue)
    {
        var oldIndex = aim.IndexOf(oldValue);
        while (oldIndex >= 0)
        {
            aim[oldIndex] = newValue;
            oldIndex = aim.IndexOf(oldValue);
        }
    }

    public static IEnumerable<T> RemoveAll<T>(this ICollection<T> aim, Func<T, bool> predicate)
    {
        var items = aim.Where(predicate).ToArray();

        foreach (var item in items)
        {
            aim.Remove(item);
        }

        return items;
    }

    /// <summary>
    /// swap the values of two indexes in the array
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="aim"></param>
    /// <param name="index1"></param>
    /// <param name="index2"></param>
    public static void Swap<T>(this IList<T> aim, Index index1, Index index2)
    {
        if (index1.Equals(index2))
        {
            return;
        }

        (aim[index2], aim[index1]) = (aim[index1], aim[index2]);
    }

    /// <summary>
    /// concatenates the members of a collection, using the specified separator between each member.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="aim"></param>
    /// <param name="separator"></param>
    /// <returns></returns>
    public static string JoinStr<T>(this IEnumerable<T> aim, string separator = ",")
    {
        if (aim.IsNull())
        {
            return string.Empty;
        }

        if (separator.IsEmpty())
        {
            return string.Concat(aim);
        }

        return string.Join(separator, aim);
    }

    /// <summary>
    /// convert to enumerable
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static IEnumerable<T> AsEnumerable<T>(this IEnumerator<T> aim)
    {
        while (aim.MoveNext())
        {
            yield return aim.Current;
        }
    }

    /// <summary>
    /// loop traversal
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="aim"></param>
    /// <param name="action"></param>
    public static void ForEach<T>(this IEnumerable<T> aim, Action<T> action)
    {
        if (aim.IsNull() || action == null)
        {
            return;
        }

        foreach (var item in aim)
        {
            action.Invoke(item);
        }
    }

    /// <summary>
    /// loop traversal
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="aim"></param>
    /// <param name="action"></param>
    public static void ForEach<T>(this T[] aim, Action<T> action)
    {
        if (aim.IsNull() || action == null)
        {
            return;
        }

        Array.ForEach(aim, action);
    }

    public static RangeEnumerator GetEnumerator(this Range range)
    {
        return new RangeEnumerator(range);
    }

    public static RangeEnumerator GetEnumerator(this int aim)
    {
        return (0..aim).GetEnumerator();
    }

    public static List<T> ToListReflector<T>(this DataTable aim)
        where T : class, new()
    {
        return DataTableBuilder<T>.Load(aim);
    }

    /// <summary>
    /// 求集合的笛卡尔积
    /// </summary>
    public static IEnumerable<IEnumerable<T>> Cartesian<T>(this IEnumerable<IEnumerable<T>> sequences)
    {
        IEnumerable<IEnumerable<T>> tmp = [[]];
        return sequences.Aggregate(tmp, (acc, seq) => from a in acc
                                                      from s in seq
                                                      select a.Concat([s]));
    }

    #region Array
    public static void Clear(this Array aim)
    {
        Array.Clear(aim);
    }

    public static T? Find<T>(this T[] aim, Predicate<T> match)
    {
        return Array.Find(aim, match);
    }

    public static T? FindLast<T>(this T[] aim, Predicate<T> match)
    {
        return Array.FindLast(aim, match);
    }

    public static int FindIndex<T>(this T[] aim, Predicate<T> match)
    {
        return Array.FindIndex(aim, match);
    }

    public static int FindLastIndex<T>(this T[] aim, Predicate<T> match)
    {
        return Array.FindLastIndex(aim, match);
    }

    public static bool Exists<T>(this T[] aim, Predicate<T> match)
    {
        return Array.Exists(aim, match);
    }

    public static T[] FindAll<T>(this T[] aim, Predicate<T> match)
    {
        return Array.FindAll(aim, match);
    }

    public static void Fill<T>(this T[] aim, T value)
    {
        Array.Fill(aim, value);
    }

    public static bool TrueForAll<T>(this T[] aim, Predicate<T> match)
    {
        return Array.TrueForAll(aim, match);
    }

    public static int IndexOf(this Array aim, object value)
    {
        return Array.IndexOf(aim, value);
    }

    public static int LastIndexOf(this Array aim, object value)
    {
        return Array.LastIndexOf(aim, value);
    }

    public static void Reverse<T>(this T[] aim)
    {
        Array.Reverse(aim);
    }

    public static void Reverse(this Array aim)
    {
        Array.Reverse(aim);
    }

    public static void Sort<T>(this T[] aim)
    {
        Array.Sort(aim);
    }

    public static void Sort(this Array aim)
    {
        Array.Sort(aim);
    }

    public static void Resize<T>(this T[] aim, int size)
    {
        Array.Resize(ref aim, size);
    }
    #endregion

    #region Search
    /// <summary>
    /// judging that it does not have any data
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="aim"></param>
    /// <param name="predicate"></param>
    /// <returns></returns>
    public static bool IsNull<T>([NotNullWhen(false)] this IEnumerable<T>? aim, Func<T, bool>? predicate = null)
    {
        return aim == null || (predicate == null ? !aim.Any() : !aim.Any(predicate));
    }

    public static bool IsNotNull<T>([NotNullWhen(true)] this IEnumerable<T>? aim, Func<T, bool>? predicate = null)
    {
        return !aim.IsNull(predicate);
    }

    public static bool IsNull<T>([NotNullWhen(false)] this ICollection<T>? aim)
    {
        return aim == null || aim.Count == 0;
    }

    public static bool IsNotNull<T>([NotNullWhen(true)] this ICollection<T>? aim)
    {
        return !aim.IsNull();
    }

    /// <summary>
    /// judging that it does not have any data
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static bool IsNull([NotNullWhen(false)] this DataTable? aim)
    {
        return aim == null || aim.Rows.Count == 0;
    }

    public static bool IsNull([NotNullWhen(false)] this DataSet? aim)
    {
        return aim == null || aim.Tables.Count == 0 || aim.Tables[0].IsNull();
    }

    public static bool ContainsAll<T>(this IEnumerable<T> aim, params T[] values)
    {
        foreach (var item in values)
        {
            if (!aim.Contains(item))
            {
                return false;
            }
        }

        return true;
    }

    public static bool ContainsAll<T>(this IEnumerable<T> aim, IEqualityComparer<T> comparer, params T[] values)
    {
        foreach (var item in values)
        {
            if (!aim.Contains(item, comparer))
            {
                return false;
            }
        }

        return true;
    }

    public static bool ContainsAny<T>(this IEnumerable<T> aim, params T[] values)
    {
        foreach (var item in values)
        {
            if (aim.Contains(item))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsAny<T>(this IEnumerable<T> aim, IEqualityComparer<T> comparer, params T[] values)
    {
        foreach (var item in values)
        {
            if (aim.Contains(item, comparer))
            {
                return true;
            }
        }

        return false;
    }
    #endregion

    #region Dictionary
    public static TValue? GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
    {
        return dictionary.TryGetValue(key, out var obj) ? obj : default;
    }

    public static TValue? GetOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
    {
        return dictionary.TryGetValue(key, out var obj) ? obj : default;
    }
    #endregion

    #region RandomSort
    public static void RandomSort<T>(this IList<T> array)
    {
        //random sort algorithm for array: two randomly selected position, two position on the value of the exchange
        // times, the length of the array is used here as the exchange number
        var count = array.Count;

        // Fisher-Yates随机置换算法
        for (var i = count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            array.Swap(i, j);
        }
    }
    #endregion

    #endregion
}

public struct RangeEnumerator
{
    private readonly int _end;

    public RangeEnumerator(Range range)
    {
        if (range.End.IsFromEnd)
        {
            throw new NotSupportedException();
        }

        Current = range.Start.Value - 1;
        _end = range.End.Value;
    }

    public int Current { get; private set; }

    public bool MoveNext()
    {
        var cur = Current + 1;
        if (cur < _end)
        {
            Current = cur;
            return true;
        }

        return false;
    }
}

file static class DataTableBuilder<T>
    where T : class
{
    private static readonly ConcurrentDictionary<string, LoadRow> _cache = new();

    private static readonly MethodInfo? _getMethod = typeof(DataRow).GetMethod("get_Item", [typeof(int)]);

    private static readonly MethodInfo? _isMethod = typeof(DataRow).GetMethod("IsNull", [typeof(int)]);

    private delegate T LoadRow(DataRow row);

    public static List<T> Load(DataTable table)
    {
        if (table.IsNull())
        {
            return [];
        }

        var key = string.Join(",", table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
        var loader = _cache.GetOrAdd(key, _ => BuildLoader(table));

        var list = new List<T>(table.Rows.Count);
        foreach (DataRow row in table.Rows)
        {
            var data = loader(row);
            list.Add(data);
        }

        return list;
    }

    private static LoadRow BuildLoader(DataTable table)
    {
        var createEntity = new DynamicMethod("DynamicCreateEntity", typeof(T), [typeof(DataRow)], typeof(T), true);
        var generator = createEntity.GetILGenerator();
        var result = generator.DeclareLocal(typeof(T));

        generator.Emit(OpCodes.Newobj, typeof(T).GetConstructor(Type.EmptyTypes)!);
        generator.Emit(OpCodes.Stloc, result);

        foreach (var i in table.Columns.Count)
        {
            var propertyInfo = typeof(T).GetProperty(table.Columns[i].ColumnName);
            var endIfLabel = generator.DefineLabel();

            if (propertyInfo == null || propertyInfo.GetSetMethod() == null)
            {
                continue;
            }

            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldc_I4, i);
            generator.Emit(OpCodes.Callvirt, _isMethod!);
            generator.Emit(OpCodes.Brtrue, endIfLabel);
            generator.Emit(OpCodes.Ldloc, result);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldc_I4, i);
            generator.Emit(OpCodes.Callvirt, _getMethod!);
            generator.Emit(OpCodes.Unbox_Any, propertyInfo.PropertyType);
            generator.Emit(OpCodes.Callvirt, propertyInfo.GetSetMethod()!);
            generator.MarkLabel(endIfLabel);
        }

        generator.Emit(OpCodes.Ldloc, result);
        generator.Emit(OpCodes.Ret);

        return createEntity.CreateDelegate<LoadRow>();
    }
}
using System.Collections.Concurrent;

namespace BeniceSoft.Core;

internal static class DeepClonerCache
{
    private static readonly object NullSentinel = new();

    private static readonly ConcurrentDictionary<Type, object> TypeCache = new();
    private static readonly ConcurrentDictionary<Type, object> TypeCacheDeepTo = new();
    private static readonly ConcurrentDictionary<Type, object> TypeCacheShallowTo = new();
    private static readonly ConcurrentDictionary<Type, object> StructAsObjectCache = new();
    private static readonly ConcurrentDictionary<(Type From, Type To), object> TypeConvertCache = new();

    public static object? GetOrAddClass<T>(Type type, Func<Type, T?> adder)
        where T : class
        => GetOrAdd(TypeCache, type, adder);

    public static object? GetOrAddDeepClassTo<T>(Type type, Func<Type, T?> adder)
        where T : class
        => GetOrAdd(TypeCacheDeepTo, type, adder);

    public static object? GetOrAddShallowClassTo<T>(Type type, Func<Type, T?> adder)
        where T : class
        => GetOrAdd(TypeCacheShallowTo, type, adder);

    public static object? GetOrAddStructAsObject<T>(Type type, Func<Type, T?> adder)
        where T : class
        => GetOrAdd(StructAsObjectCache, type, adder);

    public static T GetOrAddConvertor<T>(Type from, Type to, Func<Type, Type, T> adder)
        where T : class
        => (T)TypeConvertCache.GetOrAdd((from, to), key => adder(key.From, key.To)!);

    public static void ClearCache()
    {
        TypeCache.Clear();
        TypeCacheDeepTo.Clear();
        TypeCacheShallowTo.Clear();
        StructAsObjectCache.Clear();
        TypeConvertCache.Clear();
    }

    private static object? GetOrAdd<T>(ConcurrentDictionary<Type, object> cache, Type type, Func<Type, T?> adder)
        where T : class
    {
        if (cache.TryGetValue(type, out var value))
        {
            return ReferenceEquals(value, NullSentinel) ? null : value;
        }

        lock (type)
        {
            if (cache.TryGetValue(type, out value))
            {
                return ReferenceEquals(value, NullSentinel) ? null : value;
            }

            var created = adder(type);
            cache[type] = created ?? NullSentinel;
            return created;
        }
    }
}

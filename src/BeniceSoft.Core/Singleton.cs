using System.Collections.Concurrent;

namespace BeniceSoft.Core;

public class Singleton
{
    /// <summary>
    /// provides access to all "singletons" 
    /// </summary>
    public static IDictionary<Type, object> AllSingletons { get; } = new ConcurrentDictionary<Type, object>();
}

public class Singleton<T> : Singleton
{
    private static T? _instance;

    public static T? Instance
    {
        get => _instance;

        set
        {
            _instance = value;
            AllSingletons[typeof(T)] = _instance!;
        }
    }

    public static IDictionary<string, object> Session { get; } = new Dictionary<string, object>();
}

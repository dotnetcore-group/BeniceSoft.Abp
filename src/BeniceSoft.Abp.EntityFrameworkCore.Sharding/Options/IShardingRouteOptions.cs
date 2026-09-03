using BeniceSoft.Core;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IShardingRouteOptions
{
    /// <summary>
    /// 添加分库路由
    /// </summary>
    /// <typeparam name="T"></typeparam>
    void AddDataSourceRoute<T>()
        where T : IDataSourceRoute;

    /// <summary>
    /// 添加分库路由
    /// </summary>
    /// <param name="routeType"></param>
    void AddDataSourceRoute(Type routeType);

    /// <summary>
    /// 添加分表路由
    /// </summary>
    /// <typeparam name="T"></typeparam>
    void AddTableRoute<T>()
        where T : ITableRoute;

    /// <summary>
    /// 添加分表路由
    /// </summary>
    /// <param name="routeType"></param>
    void AddTableRoute(Type routeType);

    /// <summary>
    /// 是否有虚拟库路由
    /// </summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    bool HasDataSourceRoute(Type entityType);

    /// <summary>
    /// 获取虚拟库路由
    /// </summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    Type GetDataSourceRoute(Type entityType);

    /// <summary>
    /// 是否有虚拟表路由
    /// </summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    bool HasTableRoute(Type entityType);

    /// <summary>
    /// 获取虚拟表路由
    /// </summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    Type GetTableRoute(Type entityType);

    /// <summary>
    /// 获取所有的分库路由类型
    /// </summary>
    /// <returns></returns>
    ISet<Type> GetDataSourceRoutes();

    /// <summary>
    /// 获取所有的分表路由类型
    /// </summary>
    /// <returns></returns>
    ISet<Type> GetTableRoutes();

    /// <summary>
    /// 添加平行表
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    bool AddParallelTable(ParallelTableNode node);

    /// <summary>
    /// 获取平行表
    /// </summary>
    /// <returns></returns>
    ISet<ParallelTableNode> GetParallelTables();
}

public class ShardingRouteOptions : IShardingRouteOptions
{
    private readonly Dictionary<Type, Type> _dataSourceRoutes = [];
    private readonly Dictionary<Type, Type> _tableRoutes = [];
    private readonly HashSet<ParallelTableNode> _parallelTables = [];

    /// <summary>
    /// 添加分库路由
    /// </summary>
    /// <typeparam name="TRoute"></typeparam>
    public void AddDataSourceRoute<TRoute>()
        where TRoute : IDataSourceRoute
    {
        var routeType = typeof(TRoute);
        AddDataSourceRoute(routeType);
    }

    public void AddDataSourceRoute(Type routeType)
    {
        if (!routeType.IsDataSourceRoute())
        {
            throw new ShardingInvalidOperationException(routeType.FullName ?? routeType.Name);
        }
        //获取类型
        var route = routeType.GetInterfaces().Find(it => it.IsInterface && it.IsGenericType && it.GetGenericTypeDefinition() == typeof(IDataSourceRoute<>) && it.GetGenericArguments().Length != 0);

        if (route == null)
        {
            throw new ArgumentException($"add sharding route type error not assignable from {nameof(IDataSourceRoute<object>)}.");
        }

        var entityType = route.GetGenericArguments()[0];
        if (entityType == null)
        {
            throw new ArgumentException($"add sharding table route type error not assignable from {nameof(IDataSourceRoute<object>)}.");
        }

        _dataSourceRoutes.TryAdd(entityType, routeType);
    }
    /// <summary>
    /// 添加分表路由
    /// </summary>
    /// <typeparam name="TRoute"></typeparam>
    public void AddTableRoute<TRoute>()
        where TRoute : ITableRoute
    {
        var routeType = typeof(TRoute);
        AddTableRoute(routeType);
    }

    public void AddTableRoute(Type routeType)
    {
        if (!routeType.IsTableRoute())
        {
            throw new ShardingInvalidOperationException(routeType.FullName ?? routeType.Name);
        }

        //获取类型
        var route = routeType.GetInterfaces().Find(it => it.IsInterface && it.IsGenericType && it.GetGenericTypeDefinition() == typeof(ITableRoute<>) && it.GetGenericArguments().Length != 0);
        if (route == null)
        {
            throw new ArgumentException($"add sharding route type error not assignable from {nameof(ITableRoute<object>)}.");
        }

        var shardingEntityType = route.GetGenericArguments()[0];
        if (shardingEntityType == null)
        {
            throw new ArgumentException($"add sharding table route type error not assignable from {nameof(ITableRoute<object>)}.");
        }

        _tableRoutes.TryAdd(shardingEntityType, routeType);
    }

    public bool HasTableRoute(Type entityType)
    {
        return _tableRoutes.ContainsKey(entityType);
    }

    public Type GetTableRoute(Type entityType)
    {
        if (!_tableRoutes.TryGetValue(entityType, out var value))
        {
            throw new ArgumentException($"{entityType} not found {nameof(ITableRoute)}");
        }

        return value;
    }

    public bool HasDataSourceRoute(Type entityType)
    {
        return _dataSourceRoutes.ContainsKey(entityType);
    }

    public Type GetDataSourceRoute(Type entityType)
    {
        if (!_dataSourceRoutes.TryGetValue(entityType, out var value))
        {
            throw new ArgumentException($"{entityType} not found {nameof(IDataSourceRoute)}");
        }

        return value;
    }

    public ISet<Type> GetTableRoutes()
    {
        return _tableRoutes.Keys.ToHashSet();
    }

    public ISet<Type> GetDataSourceRoutes()
    {
        return _dataSourceRoutes.Keys.ToHashSet();
    }
    public bool AddParallelTable(ParallelTableNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return _parallelTables.Add(node);
    }
    /// <summary>
    /// 获取平行表
    /// </summary>
    /// <returns></returns>
    public ISet<ParallelTableNode> GetParallelTables()
    {
        return _parallelTables;
    }
}

public class ShardingAsRouteOptions(Action<ShardingRouteContext> factory)
{
    public Action<ShardingRouteContext> RouteFactory { get; } = factory;
}

public class ShardingAsSequenceOptions(bool sameComparer, bool sequence)
{
    public bool SameComparer { get; } = sameComparer;

    public bool Sequence { get; } = sequence;
}

public class ShardingAsSeparationOptions(bool readOnly)
{
    public bool ReadOnly { get; } = readOnly;
}

public class ShardingAsConnectionOptions(int maxQueryConnections, ConnectionMode connectionMode)
{
    public int MaxQueryConnections { get; } = maxQueryConnections;

    public ConnectionMode ConnectionMode { get; } = connectionMode;
}

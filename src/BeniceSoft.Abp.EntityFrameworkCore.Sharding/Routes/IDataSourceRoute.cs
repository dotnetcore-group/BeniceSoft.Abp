using BeniceSoft.Core;
using BeniceSoft.Core.Strategy;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IDataSourceRoute
{
    EntityMetadata EntityMetadata { get; }

    /// <summary>
    /// 分页配置
    /// </summary>
    PagedMetadata? PagedMetadata { get; }

    /// <summary>
    /// 是否启用分页配置
    /// </summary>
    bool EnablePaged { get; }

    string GetKey(object shardingKey);

    /// <summary>
    /// 根据查询条件路由返回物理数据源
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="isQuery"></param>
    /// <returns>data source name</returns>
    IReadOnlyList<string> GetRouteList(IQueryable queryable, bool isQuery);

    /// <summary>
    /// 根据值进行路由
    /// </summary>
    /// <param name="shardingKey"></param>
    /// <returns>data source name</returns>
    string GetRouteValue(object shardingKey);

    IReadOnlyList<string> GetAll();

    /// <summary>
    /// 添加数据源
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    bool Add(string name);
}

public interface IDataSourceRoute<T> : IDataSourceRoute, IEntityMetadataDataSource<T>
    where T : class
{
    IShardingPaged<T>? Create();
}

public abstract class DataSourceRoute<T> : IDataSourceRoute<T>, IEntityMetadataBinder
    where T : class
{
    private readonly OnceLock _lock = new();

    /// <summary>
    /// 启用提示路由
    /// </summary>
    protected virtual bool EnabledHint => false;

    /// <summary>
    /// 启用断言路由
    /// </summary>
    protected virtual bool EnabledAssert => false;

    public ShardingRouteContext? Current => ShardingProvider.GetRequiredService<IShardingRouteManager>().Current;

    public EntityMetadata EntityMetadata { get; private set; } = null!;

    public PagedMetadata? PagedMetadata { get; protected set; }

    public bool EnablePaged => PagedMetadata != null;

    public IShardingProvider ShardingProvider { get; private set; } = null!;

    private List<string> DoMustDataSource(IReadOnlyList<string> allDataSource, ISet<string> mustDataSources)
    {

        var dataSources = allDataSource.Where(mustDataSources.Contains).ToList();
        if (dataSources.IsNull() || dataSources.Count != mustDataSources.Count)
        {
            throw new ShardingException($"sharding data source route must error:[{EntityMetadata.EntityType.FullName}]-->[{mustDataSources.JoinStr()}]");
        }

        return dataSources;
    }

    private IReadOnlyList<string> DoHintDataSource(IReadOnlyList<string> allDataSource, ISet<string> hintDataSources)
    {
        var dataSources = allDataSource.Where(hintDataSources.Contains).ToList();
        if (dataSources.IsNull() || dataSources.Count != hintDataSources.Count)
        {
            throw new ShardingException($"sharding data source route hint error:[{EntityMetadata.EntityType.FullName}]-->[{hintDataSources.JoinStr()}]");
        }

        return GetFilterDataSource(allDataSource, dataSources);
    }

    /// <summary>
    /// 判断是调用全局还是内部断言
    /// </summary>
    /// <param name="allDataSource"></param>
    /// <param name="filterDataSource"></param>
    /// <returns></returns>
    private IReadOnlyList<string> GetFilterDataSource(IReadOnlyList<string> allDataSource, IReadOnlyList<string> filterDataSource)
    {
        IEnumerable<IDataSourceRouteAssert>? routeAsserts = null;

        var current = Current;
        var useAssertRoute = EnabledAssert && current != null && (current.TryGetAssertDataSource<T>(out routeAsserts) && routeAsserts.IsNotNull() || current.AssertAllDataSource.IsNotNull());

        if (useAssertRoute)
        {
            //最后处理断言
            if (routeAsserts.IsNotNull())
            {
                foreach (var routeAssert in routeAsserts)
                {
                    routeAssert.Assert(allDataSource, filterDataSource);
                }
            }

            if (current!.AssertAllDataSource.IsNotNull())
            {
                foreach (var routeAssert in current.AssertAllDataSource)
                {
                    routeAssert.Assert(allDataSource, filterDataSource);
                }
            }

            return filterDataSource;
        }
        else
        {
            return AfterDataSourceFilter(allDataSource, filterDataSource);
        }
    }

    /// <summary>
    /// 物理表过滤后
    /// </summary>
    /// <param name="allDataSource">所有的物理表</param>
    /// <param name="filterDataSource">过滤后的物理表</param>
    /// <returns></returns>
    protected virtual IReadOnlyList<string> AfterDataSourceFilter(IReadOnlyList<string> allDataSource, IReadOnlyList<string> filterDataSource)
    {
        return filterDataSource;
    }

    protected abstract IReadOnlyList<string> GetRouteList(IReadOnlyList<string> allDataSource, IQueryable queryable);

    public abstract bool Add(string name);

    public abstract void Configure(EntityMetadataDataSourceBuilder<T> builder);

    public virtual IShardingPaged<T>? Create()
    {
        return null;
    }

    public abstract IReadOnlyList<string> GetAll();

    public abstract string GetKey(object shardingKey);

    public IReadOnlyList<string> GetRouteList(IQueryable queryable, bool isQuery)
    {
        var allDataSource = GetAll();
        if (!isQuery)
        {
            //后拦截器
            return AfterDataSourceFilter(allDataSource, GetRouteList(allDataSource, queryable));
        }

        //强制路由不经过断言
        if (EnabledHint)
        {
            if (Current != null)
            {
                if (Current.TryGetMustDataSource<T>(out var mustDataSources) && mustDataSources.IsNotNull())
                {
                    return DoMustDataSource(allDataSource, mustDataSources);
                }
                else if (Current.MustAllDataSource.IsNotNull())
                {
                    return DoMustDataSource(allDataSource, Current.MustAllDataSource);
                }

                if (Current.TryGetHintDataSource<T>(out var hintDataSources) && hintDataSources.IsNotNull())
                {
                    return DoHintDataSource(allDataSource, hintDataSources);
                }
                else if (Current.HintAllDataSource.IsNotNull())
                {
                    return DoHintDataSource(allDataSource, Current.HintAllDataSource);
                }
            }
        }

        var filterDataSources = GetRouteList(allDataSource, queryable);
        return GetFilterDataSource(allDataSource, filterDataSources);
    }

    public abstract string GetRouteValue(object shardingKey);

    public void Initialize(EntityMetadata entityMetadata, IShardingProvider shardingProvider)
    {
        if (!_lock.IsAcquired)
        {
            throw new ShardingInvalidOperationException("Already Initialize");
        }

        ShardingProvider = shardingProvider;
        EntityMetadata = entityMetadata;

        var paged = Create();
        if (paged != null)
        {
            var data = new PagedMetadata();
            var paginationBuilder = new PagedBuilder<T>(data);
            paged.Configure(paginationBuilder);
        }
    }
}

public abstract class DataSourceRoute<T, TKey> : DataSourceRoute<T>
    where T : class
{
    protected override IReadOnlyList<string> GetRouteList(IReadOnlyList<string> allDataSource, IQueryable queryable)
    {
        //获取路由后缀表达式
        var expression = queryable.GetRouteExpression(EntityMetadata, GetRouteFactory, GetCompareValue, false);
        //表达式缓存编译
        // var filter = CachingCompile(routeParseExpression);
        var filter = expression.GetRoutePredicate();
        //通过编译结果进行过滤
        var dataSources = allDataSource.Where(o => filter(o)).ToList();
        return dataSources;
    }

    public virtual object GetCompareValue(object shardingKey, string? propertyName)
    {
        return shardingKey;
    }

    protected virtual Func<string, bool> GetRouteFactory(TKey shardingKey, ShardingOperator shardingOperator)
    {
        if (shardingOperator != ShardingOperator.Equal)
        {
            return t => true;
        }

        var tail = GetKey(shardingKey!);
        return t => t == tail;
    }

    public virtual Func<string, bool> GetAdditionalRouteFactory(object shardingKey, ShardingOperator shardingOperator, string? propertyName)
    {
        throw new NotImplementedException(propertyName ?? nameof(GetAdditionalRouteFactory));
    }

    /// <summary>
    /// 如何路由到具体表 shardingKeyValue:分表的值, 返回结果:如果返回true表示返回该表 第一个参数 tail 第二参数是否返回该物理表
    /// </summary>
    /// <param name="shardingKey">分表的值</param>
    /// <param name="shardingOperator">操作</param>
    /// <param name="propertyName">操作</param>
    /// <returns>如果返回true表示返回该表 第一个参数 tail 第二参数是否返回该物理表</returns>
    protected virtual Func<string, bool> GetRouteFactory(object shardingKey,
        ShardingOperator shardingOperator, string? propertyName)
    {
        if (EntityMetadata.DataSourceProperty!.Name == propertyName)
        {
            return GetRouteFactory((TKey)shardingKey, shardingOperator);
        }
        else
        {
            return GetAdditionalRouteFactory(shardingKey, shardingOperator, propertyName!);
        }
    }

    public override string GetRouteValue(object shardingKey)
    {
        var allDataSource = GetAll();
        var shardingKeyToDataSource = GetKey(shardingKey!);

        var dataSources = allDataSource.Where(o => o == shardingKeyToDataSource).ToList();
        if (dataSources.IsNull())
        {
            throw new ShardingException($"sharding key route not match {EntityMetadata.EntityType} -> [{EntityMetadata.DataSourceProperty!.Name}] ->【{shardingKey}】 all data sources ->[{allDataSource.JoinStr()}]");
        }

        if (dataSources.Count > 1)
        {
            throw new ShardingException($"more than one route match data source:{allDataSource.JoinStr()}");
        }

        return dataSources[0];
    }
}

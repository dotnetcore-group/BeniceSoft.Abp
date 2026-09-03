using BeniceSoft.Core;
using BeniceSoft.Core.Strategy;
using Microsoft.Extensions.Logging;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface ITableRoute
{
    EntityMetadata EntityMetadata { get; }

    /// <summary>
    /// 分页配置
    /// </summary>
    PagedMetadata? PagedMetadata { get; }

    /// <summary>
    /// 是否启用智能分页
    /// </summary>
    bool EnablePaged { get; }

    /// <summary>
    /// 查询配置
    /// </summary>
    EntityQueryMetadata? EntityQueryMetadata { get; }

    /// <summary>
    /// 是否启用表达式分片配置
    /// </summary>
    bool EnableQuery { get; }

    string GetKey(object shardingKey);

    /// <summary>
    /// 根据查询条件路由返回物理表
    /// </summary>
    /// <param name="routeResult"></param>
    /// <param name="queryable"></param>
    /// <param name="isQuery"></param>
    /// <returns></returns>
    IReadOnlyList<TableRouteUnit> GetRouteList(DataSourceRouteResult routeResult, IQueryable queryable, bool isQuery);

    /// <summary>
    /// 根据值进行路由
    /// </summary>
    /// <param name="routeResult"></param>
    /// <param name="shardingKey"></param>
    /// <returns></returns>
    TableRouteUnit GetRouteValue(DataSourceRouteResult routeResult, object shardingKey);

    /// <summary>
    /// 获取所有的目前数据库存在的尾巴,每次路由都会调用
    /// 请不要在此处添加过于复杂的操作
    /// get all tails in the db
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<string> GetTails();
}

public interface ITableRoute<T> : ITableRoute, IEntityMetadataTable<T>
    where T : class
{
    /// <summary>
    /// 返回null就是表示不开启分页配置
    /// </summary>
    /// <returns></returns>
    IShardingPaged<T>? CreatePaged();

    /// <summary>
    /// 配置查询
    /// </summary>
    /// <returns></returns>
    IEntityQuery<T>? CreateEntityQuery();
}

public abstract class TableRoute<T> : ITableRoute<T>, IEntityMetadataBinder
    where T : class
{
    private readonly OnceLock _lock = new();

    /// <summary>
    /// 启用提示路由(业务若通过 AsRoute 使用 Must/Hint，必须为 true，否则不生效)
    /// </summary>
    protected virtual bool EnabledHint => false;

    /// <summary>
    /// 启用断言路由(业务若通过 AsRoute 注册 AssertTable/AssertDataSource，必须为 true，否则不生效;在路由结果算出后校验物理表/库是否符合预期)
    /// </summary>
    protected virtual bool EnabledAssert => false;

    /// <summary>
    /// 路由是否忽略数据源
    /// </summary>
    protected virtual bool IgnoreDataSource => true;

    /// <summary>
    /// 路由数据源和表后缀连接符
    /// </summary>
    protected virtual string Separator => ".";

    public ShardingRouteContext? Current => ShardingProvider.GetRequiredService<IShardingRouteManager>().Current;

    public EntityMetadata EntityMetadata { get; private set; } = null!;

    /// <summary>
    /// 查询配置
    /// </summary>
    public EntityQueryMetadata? EntityQueryMetadata { get; private set; }

    public PagedMetadata? PagedMetadata { get; private set; }

    public bool EnablePaged => PagedMetadata != null;

    public bool EnableQuery => EntityQueryMetadata != null;

    public IShardingProvider ShardingProvider { get; private set; } = null!;

    public abstract void Configure(EntityMetadataTableBuilder<T> builder);

    public IEntityQuery<T>? CreateEntityQuery()
    {
        return null;
    }

    public virtual IShardingPaged<T>? CreatePaged()
    {
        return null;
    }

    public abstract string GetKey(object shardingKey);

    public IReadOnlyList<TableRouteUnit> GetRouteList(DataSourceRouteResult routeResult, IQueryable queryable, bool isQuery)
    {
        if (!isQuery)
        {
            //后拦截器
            return GetRouteList(routeResult, queryable);
        }

        // 强制/提示路由（Must/Hint）
        if (EnabledHint)
        {
            if (Current != null)
            {
                if (Current.TryGetMustTail<T>(out var mustTails) && mustTails != null)
                {
                    if (mustTails.Count == 0)
                    {
                        throw new ShardingException($" sharding route must error:[{EntityMetadata.EntityType.FullName}]-->[]");
                    }

                    var tails = GetTails().Where(mustTails.Contains).ToList();

                    if (tails.IsNull() || tails.Count != mustTails.Count)
                    {
                        throw new ShardingException($" sharding route must error:[{EntityMetadata.EntityType.FullName}]-->[{mustTails.JoinStr()}]");
                    }

                    var units = routeResult.Intersect.SelectMany(dataSourceName => tails.Select(tail => new TableRouteUnit(dataSourceName, tail, typeof(T)))).ToList();
                    return units;
                }

                if (Current.TryGetHintTail<T>(out var hintTails) && hintTails != null)
                {
                    if (hintTails.Count == 0)
                    {
                        throw new ShardingException($" sharding route hint error:[{EntityMetadata.EntityType.FullName}]-->[]");
                    }

                    var tails = GetTails().Where(hintTails.Contains).ToList();
                    if (tails.IsNull() || tails.Count != hintTails.Count)
                    {
                        throw new ShardingException($" sharding route hint error:[{EntityMetadata.EntityType.FullName}]-->[{hintTails.JoinStr()}]");
                    }

                    var units = routeResult.Intersect.SelectMany(dataSourceName => tails.Select(tail => new TableRouteUnit(dataSourceName, tail, typeof(T)))).ToList();
                    return GetTails(routeResult, units);
                }
            }
        }

        var filterPhysicTables = GetRouteList(routeResult, queryable);
        return GetTails(routeResult, filterPhysicTables);
    }

    /// <summary>
    /// 判断是调用全局过滤器还是调用内部断言
    /// </summary>
    /// <param name="routeResult"></param>
    /// <param name="routeUnits"></param>
    /// <returns></returns>
    private IReadOnlyList<TableRouteUnit> GetTails(DataSourceRouteResult routeResult, IReadOnlyList<TableRouteUnit> routeUnits)
    {
        IEnumerable<ITableRouteAssert>? routeAsserts = null;

        var useAssert = EnabledAssert && Current != null && Current.TryGetAssertTail<T>(out routeAsserts) && routeAsserts.IsNotNull();

        if (useAssert)
        {
            //最后处理断言
            foreach (var routeAssert in routeAsserts!)
            {
                routeAssert.Assert(routeResult, GetTails(), routeUnits);
            }

            return routeUnits;
        }
        else
        {
            //后拦截器
            return routeUnits;
        }
    }

    public abstract TableRouteUnit GetRouteValue(DataSourceRouteResult routeResult, object shardingKey);

    public abstract IReadOnlyList<string> GetTails();

    public virtual void Initialize(EntityMetadata entityMetadata, IShardingProvider shardingProvider)
    {
        if (!_lock.IsAcquired)
        {
            throw new ShardingInvalidOperationException("already Initialize");
        }

        ShardingProvider = shardingProvider;
        EntityMetadata = entityMetadata;

        var paged = CreatePaged();
        if (paged != null)
        {
            PagedMetadata = new PagedMetadata();
            var pagedBuilder = new PagedBuilder<T>(PagedMetadata);
            paged.Configure(pagedBuilder);
        }

        var entityQuery = CreateEntityQuery();
        if (entityQuery != null)
        {
            EntityQueryMetadata = new EntityQueryMetadata();
            var entityQueryBuilder = new EntityQueryBuilder<T>(EntityQueryMetadata);
            entityQuery.Configure(entityQueryBuilder);
        }
    }

    protected abstract IReadOnlyList<TableRouteUnit> GetRouteList(DataSourceRouteResult routeResult, IQueryable queryable);
}

public abstract class TableRoute<T, TKey> : TableRoute<T>
    where T : class
{
    protected override IReadOnlyList<TableRouteUnit> GetRouteList(DataSourceRouteResult routeResult, IQueryable queryable)
    {
        //获取路由后缀表达式
        var routeParseExpression = queryable.GetRouteExpression(EntityMetadata, GetRouteFactory, GetCompareValue, true);
        //表达式缓存编译
        var filter = routeParseExpression.GetRoutePredicate();
        var units = routeResult.Intersect.SelectMany(d => GetTails().Where(o => filter(FormatTableRoute(d, o))).Select(tail => new TableRouteUnit(d, tail, typeof(T)))).ToList();

        return units;
    }

    public virtual object GetCompareValue(object shardingKey, string? propertyName)
    {
        return shardingKey;
    }

    private string FormatTableRoute(string dataSource, string tableTail)
    {
        if (IgnoreDataSource)
        {
            return tableTail;
        }

        return $"{dataSource}{Separator}{tableTail}";
    }

    /// <summary>
    /// 如何路由到具体表
    /// </summary>
    /// <param name="shardingKey">分表的值</param>
    /// <param name="shardingOperator">操作</param>
    /// <param name="propertyName">分表字段</param>
    /// <returns>如果返回true表示返回该表 第一个参数 tail 第二参数是否返回该物理表</returns>
    protected virtual Func<string, bool> GetRouteFactory(
        object shardingKey,
        ShardingOperator shardingOperator,
        string? propertyName)
    {
        if (EntityMetadata.TableProperty!.Name == propertyName)
        {
            return GetRouteFactory((TKey)shardingKey, shardingOperator);
        }
        else
        {
            return GetAdditionalRouteFactory(shardingKey, shardingOperator, propertyName!);
        }
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

    public virtual Func<string, bool> GetAdditionalRouteFactory(object shardingKey, ShardingOperator shardingOperator, string propertyName)
    {
        throw new NotImplementedException(propertyName);
    }

    public override TableRouteUnit GetRouteValue(DataSourceRouteResult routeResult, object shardingKey)
    {
        if (routeResult.Intersect.Count != 1)
        {
            throw new ShardingException($"more than one route match data source:{routeResult.Intersect.JoinStr()}");
        }

        var tail = GetKey(shardingKey);

        var tails = GetTails().Where(o => o == tail).ToList();
        if (tails.IsNull())
        {
            throw new ShardingException($"sharding key route not match {EntityMetadata.EntityType} -> [{EntityMetadata.TableProperty!.Name}] -> [{shardingKey}] -> sharding key to tail :[{tail}] ->  all tails ->[{GetTails().JoinStr()}]");
        }

        if (tails.Count > 1)
        {
            throw new ShardingException($"more than one route match table:{tails.JoinStr()}");
        }

        return new TableRouteUnit(routeResult.Intersect.First(), tails[0], typeof(T));
    }
}

/// <summary>
/// 按int类型取模分表路由
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class ModIntTableRoute<T> : TableRoute<T, int>
    where T : class
{
    /// <summary>
    /// 除余取模
    /// </summary>
    /// <param name="length">后缀总长度</param>
    /// <param name="mod">被除数</param>
    /// <exception cref="ArgumentException"></exception>
    protected ModIntTableRoute(int length, int mod)
    {
        if (length < 1)
        {
            throw new ArgumentException($"{nameof(length)} less than 1 ");
        }

        if (mod < 1)
        {
            throw new ArgumentException($"{nameof(mod)} less than 1 ");
        }

        Length = length;
        Mod = mod;
    }

    protected int Length { get; }

    protected int Mod { get; }

    protected virtual char Padding => '0';

    public override IReadOnlyList<string> GetTails()
    {
        return Enumerable.Range(0, Mod).Select(o => o.ToString().PadLeft(Length, Padding)).ToList();
    }

    public override string GetKey(object shardingKey)
    {
        var key = Convert.ToInt32(shardingKey);
        return Math.Abs(key % Mod).ToString().PadLeft(Length, Padding);
    }
}

/// <summary>
/// 按long类型取模分表路由
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class ModLongTableRoute<T> : TableRoute<T, long>
    where T : class
{
    /// <summary>
    /// 除余取模
    /// </summary>
    /// <param name="length">后缀总长度</param>
    /// <param name="mod">被除数</param>
    /// <exception cref="ArgumentException"></exception>
    protected ModLongTableRoute(int length, int mod)
    {
        if (length < 1)
        {
            throw new ArgumentException($"{nameof(length)} less than 1 ");
        }

        if (mod < 1)
        {
            throw new ArgumentException($"{nameof(mod)} less than 1 ");
        }

        Length = length;
        Mod = mod;
    }

    protected int Length { get; }

    protected int Mod { get; }

    protected virtual char Padding => '0';

    public override IReadOnlyList<string> GetTails()
    {
        return Enumerable.Range(0, Mod).Select(o => o.ToString().PadLeft(Length, Padding)).ToList();
    }

    public override string GetKey(object shardingKey)
    {
        var key = Convert.ToInt64(shardingKey);
        return Math.Abs(key % Mod).ToString().PadLeft(Length, Padding);
    }
}

/// <summary>
/// 字符串取模分表路由
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class ModStringTableRoute<T> : TableRoute<T, string>
    where T : class
{
    /// <summary>
    /// 除余取模
    /// </summary>
    /// <param name="length">后缀总长度</param>
    /// <param name="mod">被除数</param>
    /// <exception cref="ArgumentException"></exception>
    protected ModStringTableRoute(int length, int mod)
    {
        if (length < 1)
        {
            throw new ArgumentException($"{nameof(length)} less than 1 ");
        }

        if (mod < 1)
        {
            throw new ArgumentException($"{nameof(mod)} less than 1 ");
        }

        Length = length;
        Mod = mod;
    }

    protected int Length { get; }

    protected int Mod { get; }

    protected virtual char Padding => '0';

    public override IReadOnlyList<string> GetTails()
    {
        return Enumerable.Range(0, Mod).Select(o => o.ToString().PadLeft(Length, Padding)).ToList();
    }

    public override string GetKey(object shardingKey)
    {
        var key = string.GetHashCode(shardingKey.ToString());
        return Math.Abs(key % Mod).ToString().PadLeft(Length, Padding);
    }
}

public abstract class TailTableRoute<T> : TableRoute<T, DateTime>, IShardingJob
    where T : class
{
    private readonly object _locker = new();
    private readonly SafeReadList<string> _tails = new();

    protected virtual int Interval { get; set; } = 10;

    /// <summary>
    /// 是否需要自动创建按时间分表的路由
    /// </summary>
    protected virtual bool CreateRoute { get; } = true;

    public string Name => $"{GetType().Name}:{EntityMetadata?.EntityType?.Name}";

    /// <summary>
    /// 重写改方法后请一起重写Interval值，比如你按月分表但是你设置cron表达式为月中的时候建表，
    /// 那么会在月中的时候 <code>DateTime.Now.AddMinutes(Interval);</code>来获取tail会导致还是当月的所以不会建表
    /// </summary>
    public virtual IEnumerable<string> CronExpression => JobExpression;

    protected abstract IEnumerable<string> JobExpression { get; }

    public bool Appended { get; } = true;

    protected abstract IReadOnlyList<string> GetStartTails();

    /// <summary>
    /// 分片开始时间请使用固定值 eg.new DateTime(20xx,xx,xx)
    /// 固定值的意思就是每次程序启动这个值都不会变化，如果你使用了Datetime.Now那么程序每次
    /// 启动获取到的这个值都是动态的是不正确的所以需要你返回一个固定值，
    /// 这个方法仅在启动时被框架调用一次用于计算
    /// </summary>
    /// <returns></returns>
    protected abstract DateTime GetBeginTime();

    protected abstract string GetTail(DateTime date);

    public bool Append(string tail)
    {
        lock (_locker)
        {
            if (!_tails.Contains(tail))
            {
                _tails.Append(tail);
                return true;
            }

            return false;
        }
    }

    public override IReadOnlyList<string> GetTails()
    {
        return _tails.CopyList;
    }

    public override void Initialize(EntityMetadata entityMetadata, IShardingProvider shardingProvider)
    {
        base.Initialize(entityMetadata, shardingProvider);
        var tails = GetStartTails();
        foreach (var tail in tails)
        {
            Append(tail);
        }
    }

    public virtual Task ExecuteAsync()
    {
        var logger = ShardingProvider.GetService<ILogger<TailTableRoute<T>>>()
            ?? throw new InvalidOperationException($"Unable to resolve logger for [{typeof(TailTableRoute<T>).Name}].");
        logger.LogDebug($"get {typeof(T).Name}'s route execute job ");

        var manager = ShardingProvider.GetRequiredService<IEntityMetadataManager>();
        var tableCreator = ShardingProvider.GetRequiredService<ITableCreator>();
        var virtualDataSource = ShardingProvider.GetRequiredService<IVirtualDataSource>();
        var routeManager = ShardingProvider.GetRequiredService<IDataSourceRouteManager>();
        var now = DateTime.Now.AddMinutes(Interval);
        var tail = GetTail(now);

        Append(tail);
        var dataSources = new HashSet<string>();
        if (manager.IsShardingDataSource(typeof(T)))
        {
            var route = routeManager.GetRoute(typeof(T));
            foreach (var dataSource in route.GetAll())
            {
                dataSources.Add(dataSource);
            }
        }
        else
        {
            dataSources.Add(virtualDataSource.DefaultDataSource);
        }

        logger.LogInformation($"auto create table data source names:[{string.Join(",", dataSources)}]");

        if (CreateRoute)
        {
            foreach (var dataSource in dataSources)
            {
                try
                {
                    logger.LogInformation($"begin table tail:[{tail}],entity:[{typeof(T).Name}]");
                    tableCreator.Create(dataSource, typeof(T), tail);
                    logger.LogInformation($"succeed table tail:[{tail}],entity:[{typeof(T).Name}]");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"{dataSource} {typeof(T).Name}'s create table error ");
                }
            }
        }

        return Task.CompletedTask;
    }

    public override string GetKey(object shardingKey)
    {
        var date = Convert.ToDateTime(shardingKey);
        return GetTail(date);
    }

    protected override Func<string, bool> GetRouteFactory(DateTime shardingKey, ShardingOperator shardingOperator)
    {
        var tail = GetTail(shardingKey);
        switch (shardingOperator)
        {
            case ShardingOperator.GreaterThan:
            case ShardingOperator.GreaterThanOrEqual:
                return t => string.Compare(t, tail, StringComparison.Ordinal) >= 0;
            case ShardingOperator.LessThan:
                {
                    //处于临界值 o=>o.time < [2021-01-01 00:00:00] 尾巴20210101不应该被返回
                    if (Critical(shardingKey))
                    {
                        return t => string.Compare(t, tail, StringComparison.Ordinal) < 0;
                    }

                    return t => string.Compare(t, tail, StringComparison.Ordinal) <= 0;
                }
            case ShardingOperator.LessThanOrEqual:
                return t => string.Compare(t, tail, StringComparison.Ordinal) <= 0;
            case ShardingOperator.Equal:
                return t => t == tail;
            default:
                return t => true;
        }
    }

    /// <summary>
    /// 临界检测
    /// </summary>
    /// <param name="shardingKey"></param>
    /// <returns></returns>
    protected abstract bool Critical(DateTime shardingKey);
}

public abstract class DayTailTableRoute<T> : TailTableRoute<T>
    where T : class
{
    protected override IEnumerable<string> JobExpression
    {
        get
        {
            return
            [
                "0 59 23 * * ?",
                "0 0 0 * * ?",
                "0 1 0 * * ?",
                "0 0 0 * * ?"
            ];
        }
    }

    protected override string GetTail(DateTime date)
    {
        return $"{date:yyyyMMdd}";
    }

    protected override IReadOnlyList<string> GetStartTails()
    {
        var beginTime = GetBeginTime().Date;

        var tails = new List<string>();
        //提前创建表
        var now = DateTimeOffset.Now.Date;
        if (beginTime > now)
        {
            throw new ArgumentException("begin time error");
        }

        var current = beginTime;
        while (current <= now)
        {
            var tail = GetTail(current);
            tails.Add(tail);
            current = current.AddDays(1);
        }

        return tails;
    }

    protected override bool Critical(DateTime shardingKey)
    {
        var date = shardingKey.Date;
        return date == shardingKey;
    }
}

public abstract class WeekTailTableRoute<T> : TailTableRoute<T>
    where T : class
{
    protected override IEnumerable<string> JobExpression
    {
        get
        {
            return
            [
                "0 59 23 ? * 1",
                "0 0 0 ? * 2",
                "0 1 0 ? * 2"
            ];
        }
    }

    protected override string GetTail(DateTime date)
    {
        var monday = DateTimeUtils.FirstDayOfWeek(date);
        var sunday = DateTimeUtils.LastDayOfWeek(date);
        return $"{monday:yyyyMMdd}_{sunday:dd}";
    }

    protected override IReadOnlyList<string> GetStartTails()
    {
        var beginTime = DateTimeUtils.FirstDayOfWeek(GetBeginTime());
        var tails = new List<string>();

        var now = DateTimeOffset.Now.Date;
        if (beginTime > now)
        {
            throw new ArgumentException("begin time error");
        }

        var current = beginTime;
        while (current <= now)
        {
            var tail = GetTail(current);
            tails.Add(tail);
            current = current.AddDays(7);
        }

        return tails;
    }

    protected override bool Critical(DateTime shardingKey)
    {
        var monday = DateTimeUtils.FirstDayOfWeek(shardingKey);
        return monday == shardingKey;
    }
}

/// <summary>
/// 按月分表路由
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class MonthTailTableRoute<T> : TailTableRoute<T>
    where T : class
{
    protected override IEnumerable<string> JobExpression
    {
        get
        {
            return
            [
                "0 59 23 28,29,30,31 * ?",  // 每月 28–31 日 23:59:00
                "0 0 0 1 * ?",              // 每月 1 日 00:00:00
                "0 1 0 1 * ?",              // 每月 1 日 00:01:00
            ];
        }
    }

    protected override string GetTail(DateTime date)
    {
        return $"{date:yyyyMM}";
    }

    protected override IReadOnlyList<string> GetStartTails()
    {
        var beginTime = DateTimeUtils.FirstDayOfMonth(GetBeginTime());
        var tails = new List<string>();

        var now = DateTimeUtils.FirstDayOfMonth(DateTimeOffset.Now.DateTime);
        if (beginTime > now)
        {
            throw new ArgumentException("begin time error");
        }

        var current = beginTime;
        while (current <= now)
        {
            var tail = GetTail(current);
            tails.Add(tail);
            current = current.AddMonths(1);
        }

        return tails;
    }

    protected override bool Critical(DateTime shardingKey)
    {
        var month = DateTimeUtils.FirstDayOfMonth(shardingKey);
        return month == shardingKey;
    }
}

/// <summary>
/// 按年分表路由
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class YearTailTableRoute<T> : TailTableRoute<T>
    where T : class
{
    protected override IEnumerable<string> JobExpression
    {
        get
        {
            return
            [
                "0 59 23 31 12 ?",
                "0 0 0 1 1 ?",
                "0 1 0 1 1 ?"
            ];
        }
    }

    protected override string GetTail(DateTime date)
    {
        return $"{date:yyyy}";
    }

    protected override IReadOnlyList<string> GetStartTails()
    {
        var beginTime = new DateTime(GetBeginTime().Year, 1, 1);
        var tails = new List<string>();

        var now = new DateTime(DateTimeOffset.Now.Year, 1, 1);
        if (beginTime > now)
        {
            throw new ArgumentException("begin time error");
        }

        var current = beginTime;
        while (current <= now)
        {
            var tail = GetTail(current);
            tails.Add(tail);
            current = current.AddYears(1);
        }

        return tails;
    }

    protected override bool Critical(DateTime shardingKey)
    {
        var year = new DateTime(shardingKey.Year, 1, 1);
        return year == shardingKey;
    }
}

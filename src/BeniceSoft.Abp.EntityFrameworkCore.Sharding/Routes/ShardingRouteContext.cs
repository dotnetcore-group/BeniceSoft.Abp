using System.ComponentModel;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public sealed class ShardingRouteContext
{
    #region 分库提示路由
    /// <summary>
    /// 强制路由直接返回对应的后缀表
    /// </summary>
    public Dictionary<Type, HashSet<string>> MustDataSource { get; } = [];

    public ISet<string> MustAllDataSource { get; } = new HashSet<string>();

    /// <summary>
    /// 提示路由会经过断言的强制路由
    /// </summary>
    public Dictionary<Type, HashSet<string>> HintDataSource { get; } = [];

    public ISet<string> HintAllDataSource { get; } = new HashSet<string>();

    /// <summary>
    /// 断言
    /// </summary>
    public Dictionary<Type, LinkedList<IDataSourceRouteAssert>> AssertDataSource { get; } = [];

    public LinkedList<IDataSourceRouteAssert> AssertAllDataSource { get; } = new();
    #endregion

    #region 分表提示路由
    /// <summary>
    /// 强制路由直接返回对应的后缀表
    /// </summary>
    public Dictionary<Type, HashSet<string>> MustTable { get; } = [];

    /// <summary>
    /// 提示路由会经过断言的强制路由
    /// </summary>
    public Dictionary<Type, HashSet<string>> HintTable { get; } = [];

    /// <summary>
    /// 断言
    /// </summary>
    public Dictionary<Type, LinkedList<ITableRouteAssert>> AssertTable { get; } = [];
    #endregion
}

/// <summary>
/// 构造函数
/// </summary>
/// <param name="accessor"></param>
/// <param name="previous"></param>
public sealed class ShardingRouteScope(IShardingRouteAccessor accessor, ShardingRouteContext? previous) : IDisposable
{

    /// <summary>
    /// 分表配置访问器
    /// </summary>
    public IShardingRouteAccessor Accessor { get; } = accessor;

    /// <summary>
    /// 回收：恢复进入 scope 前的外层 Context（嵌套时不为 null）
    /// </summary>
    public void Dispose()
    {
        Accessor.Context = previous;
        GC.SuppressFinalize(this);
    }
}

internal sealed class ShardingQueryScope : IDisposable
{
    private readonly ShardingRouteScope? _scope;
    private readonly bool _has;

    public ShardingQueryScope(IPrepareParseResult result, IShardingRouteManager manager)
    {
        _has = result.RouteFactory != null;
        if (_has)
        {
            var asRoute = result.RouteFactory;
            if (asRoute != null)
            {
                _scope = manager.CreateScope();
                asRoute.Invoke(manager.Current!);
            }
        }
    }

    public void Dispose()
    {
        if (_has)
        {
            _scope?.Dispose();
        }
    }
}

/// <summary>
/// 分片条件比较符
/// </summary>
public enum ShardingOperator
{
    /// <summary>
    /// 未知操作符
    /// </summary>
    [Description("??")]
    UnKnown,

    /// <summary>
    /// 大于
    /// </summary>
    [Description(">")]
    GreaterThan,

    /// <summary>
    /// 大于等于
    /// </summary>
    [Description(">=")]
    GreaterThanOrEqual,

    /// <summary>
    /// 小于
    /// </summary>
    [Description("<")]
    LessThan,

    /// <summary>
    /// 小于等于
    /// </summary>
    [Description("<=")]
    LessThanOrEqual,

    /// <summary>
    /// 等于
    /// </summary>
    [Description("==")]
    Equal,

    /// <summary>
    /// 不等于
    /// </summary>
    [Description("!=")]
    NotEqual,

    /// <summary>
    /// like 类似 contains
    /// </summary>
    [Description("%w%")]
    AllLike,

    /// <summary>
    /// like 类似 start with
    /// </summary>
    [Description("w%")]
    StartLike,

    /// <summary>
    /// like 类似 end with
    /// </summary>
    [Description("%w")]
    EndLike
}

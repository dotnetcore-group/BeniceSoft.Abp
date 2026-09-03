using BeniceSoft.Core;
using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal static class ShardingMergeExecutor
{
    public static T Execute<T>(StreamMergeContext streamMergeContext,
        IMergeExecutor<T> executor, bool async, IEnumerable<ISqlRouteUnit> sqlRouteUnits,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(streamMergeContext, executor, async, sqlRouteUnits, cancellationToken).GetResult();
    }

    public static async Task<T> ExecuteAsync<T>(StreamMergeContext context, IMergeExecutor<T> executor, bool async, IEnumerable<ISqlRouteUnit> units, CancellationToken cancellationToken = default)
    {
        var groups = ExecuteCore<T>(context, executor, async, units, cancellationToken).ToArray();
        var results = (await TaskHelper.WhenAllFastFail(groups)).SelectMany(o => o).ToList();
        if (results.IsNull())
        {
            throw new ShardingException("sharding execute result empty");
        }

        var streamMerge = executor.GetShardingMerger().StreamMerge(results);
        return streamMerge;
    }

    private static Task<List<T>>[] ExecuteCore<T>(StreamMergeContext context, IMergeExecutor<T> executor, bool async, IEnumerable<ISqlRouteUnit> units, CancellationToken cancellationToken = default)
    {
        var waitTaskQueue = OrderTableTails(context, units).GroupBy(o => o.DataSource).Select(o => GetSqlGroups(context, o)).Select(unit =>
        {
            return Task.Run(async () =>
            {
                if (context.UseMerge)
                {
                    var manager = context.RuntimeContext.MergeManager;
                    using (manager.CreateScope(((EmptySqlRouteUnit)unit.Groups[0].Groups[0].RouteUnit).RouteResults))
                    {
                        return await executor.ExecuteAsync(async, unit,
                            cancellationToken);
                    }
                }
                else
                {
                    return await executor.ExecuteAsync(async, unit,
                        cancellationToken);
                }
            }, cancellationToken);
        }).ToArray();
        return waitTaskQueue;
    }

    /// <summary>
    /// 顺序查询从重排序
    /// </summary>
    /// <param name="context"></param>
    /// <param name="units"></param>
    /// <returns></returns>
    private static IEnumerable<ISqlRouteUnit> OrderTableTails(StreamMergeContext context, IEnumerable<ISqlRouteUnit> units)
    {
        if (context.Sequence)
        {
            return context.SameTailComparer ? units.OrderBy(o => o.RouteResult.ReplaceTables.First().Tail, context.TailComparer) : units.OrderByDescending(o => o.RouteResult.ReplaceTables.First().Tail, context.TailComparer);
        }

        return units;
    }

    /// <summary>
    /// 每个数据源下的分表结果按 maxQueryConnectionsLimit 进行组合分组每组大小 maxQueryConnectionsLimit
    /// ConnectionModeEnum为用户配置或者系统自动计算,哪怕是用户指定也是按照maxQueryConnectionsLimit来进行分组。
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sqlGroups"></param>
    /// <returns></returns>
    private static DataSourceMergerSqlUnit GetSqlGroups(StreamMergeContext context,
        IGrouping<string, ISqlRouteUnit> sqlGroups)
    {
        var limit = context.MaxQueryConnections;
        var sqlCount = sqlGroups.Count();

        ////根据用户配置单次查询期望并发数
        //int exceptCount =
        //    Math.Max(
        //        0 == sqlCount % maxQueryConnectionsLimit
        //            ? sqlCount / maxQueryConnectionsLimit
        //            : sqlCount / maxQueryConnectionsLimit + 1, 1);
        //计算应该使用那种链接模式
        var mode = context.GetConnectionMode(sqlCount);

        //将SqlExecutorUnit进行分区,每个区maxQueryConnectionsLimit个
        //[1,2,3,4,5,6,7],maxQueryConnectionsLimit=3,结果就是[[1,2,3],[4,5,6],[7]]
        var partitions = sqlGroups.Select(o => new MergerSqlUnit(mode, o)).Partition(limit);

        var groups = partitions.Select(o => new MergerSqlGroup<MergerSqlUnit>(mode, o)).ToList();
        return new DataSourceMergerSqlUnit(mode, groups);
    }
}

internal static class ShardingQueryableMethods
{
    static ShardingQueryableMethods()
    {
        var groups = typeof(Queryable).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public).GroupBy<MethodInfo, string>(mi => mi.Name).ToDictionary<IGrouping<string, MethodInfo>, string, List<MethodInfo>>(e => e.Key, l => [.. l]);

        var typeArray = new Type[10]
        {
            typeof (int),
            typeof (int?),
            typeof (long),
            typeof (long?),
            typeof (float),
            typeof (float?),
            typeof (double),
            typeof (double?),
            typeof (decimal),
            typeof (decimal?)
        };

        AsQueryable = GetMethod(nameof(AsQueryable), 1, types => [typeof(IEnumerable<>).MakeGenericType(types[0])]);

        LongCounMethod = GetMethod("LongCount", 1, types => [typeof(IQueryable<>).MakeGenericType(types[0])]);

        SelectMethod = GetMethod(nameof(Queryable.Select), 2, types => [typeof(IQueryable<>).MakeGenericType(types[0]), typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(types[0], types[1]))]);

        foreach (var type in typeArray)
        {
            AverageWithoutMethods[type] = GetMethod("Average", 0, types => [typeof(IQueryable<>).MakeGenericType(type)]);

            AverageMethods[type] = GetMethod("Average", 1, types => [typeof(IQueryable<>).MakeGenericType(types[0]), typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(types[0], type))]);

            SumWithoutMethods[type] = GetMethod("Sum", 0, types => [typeof(IQueryable<>).MakeGenericType(type)]);

            SumMethods[type] = GetMethod("Sum", 1, types => [typeof(IQueryable<>).MakeGenericType(types[0]), typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(types[0], type))]);
        }

        MethodInfo GetMethod(string name, int genericParameterCount, Func<Type[], Type[]> factory)
        {
            return groups[name].Single<MethodInfo>(mi => (genericParameterCount == 0 && !mi.IsGenericMethod || mi.IsGenericMethod && mi.GetGenericArguments().Length == genericParameterCount) && mi.GetParameters().Select<ParameterInfo, Type>(e => e.ParameterType).SequenceEqual<Type>(factory(mi.IsGenericMethod ? mi.GetGenericArguments() : [])));
        }
    }

    private static Dictionary<Type, MethodInfo> AverageWithoutMethods { get; } = [];

    private static Dictionary<Type, MethodInfo> AverageMethods { get; } = [];

    private static Dictionary<Type, MethodInfo> SumWithoutMethods { get; } = [];

    private static Dictionary<Type, MethodInfo> SumMethods { get; } = [];

    internal static MethodInfo AsQueryable { get; }

    internal static MethodInfo LongCounMethod { get; }

    internal static MethodInfo SelectMethod { get; }

    internal static MethodInfo GetSumMethod(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return SumWithoutMethods[type];
    }
}

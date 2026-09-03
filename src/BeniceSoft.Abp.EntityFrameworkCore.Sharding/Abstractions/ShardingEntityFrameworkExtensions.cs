using BeniceSoft.Core;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public static class ShardingEntityFrameworkExtensions
{
    internal static readonly MethodInfo UseMergeMethod = typeof(ShardingEntityFrameworkExtensions).GetMethod(nameof(UseMerge))!;

    public static IQueryable<T> UseMerge<T>(this IQueryable<T> source)
    {
        return source.Provider is EntityQueryProvider ? source.Provider.CreateQuery<T>(Expression.Call(null, UseMergeMethod.MakeGenericMethod(typeof(T)), source.Expression)) : source;
    }

    internal static readonly MethodInfo AsRouteMethod = typeof(ShardingEntityFrameworkExtensions).GetMethod(nameof(AsRoute), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static IQueryable<T> AsRoute<T>(this IQueryable<T> source, ShardingAsRouteOptions options)
    {
        return source.Provider is EntityQueryProvider ? source.Provider.CreateQuery<T>(Expression.Call(null, AsRouteMethod.MakeGenericMethod(typeof(T)), source.Expression, Expression.Constant(options))) : source;
    }

    public static IQueryable<T> AsRoute<T>(this IQueryable<T> source, Action<ShardingRouteContext> routeFactory)
    {
        ArgumentNullException.ThrowIfNull(routeFactory);

        var options = new ShardingAsRouteOptions(routeFactory);

        return source.AsRoute(options);
    }

    internal static readonly MethodInfo AsConnectionMethod = typeof(ShardingEntityFrameworkExtensions).GetMethod(nameof(AsConnection), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static IQueryable<T> AsConnection<T>(this IQueryable<T> source, ShardingAsConnectionOptions options)
    {
        return source.Provider is EntityQueryProvider ? source.Provider.CreateQuery<T>(Expression.Call(null, AsConnectionMethod.MakeGenericMethod(typeof(T)), source.Expression, Expression.Constant(options))) : source;
    }

    public static IQueryable<T> AsConnection<T>(this IQueryable<T> source, int limit, ConnectionMode mode = ConnectionMode.Automatic)
    {
        if (limit < 1)
        {
            throw new ArgumentException($"{nameof(limit)} should >=1");
        }

        var options = new ShardingAsConnectionOptions(limit, mode);
        return source.AsConnection(options);
    }

    internal static readonly MethodInfo AsSequenceMethod = typeof(ShardingEntityFrameworkExtensions).GetMethod(nameof(AsSequence), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static IQueryable<T> AsSequence<T>(this IQueryable<T> source, ShardingAsSequenceOptions options)
    {
        return source.Provider is EntityQueryProvider ? source.Provider.CreateQuery<T>(Expression.Call(null, AsSequenceMethod.MakeGenericMethod(typeof(T)), source.Expression, Expression.Constant(options))) : source;
    }

    /// <summary>
    /// 使用顺序查询，仅支持单表
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="sameComparer">查询排序方式是否和表后缀一样</param>
    /// <returns></returns>
    public static IQueryable<T> AsSequence<T>(this IQueryable<T> source, bool sameComparer)
    {
        var options = new ShardingAsSequenceOptions(sameComparer, true);
        return source.AsSequence(options);
    }

    /// <summary>
    /// 不启用顺序查询
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <returns></returns>
    public static IQueryable<T> AsNoSequence<T>(this IQueryable<T> source)
    {
        var options = new ShardingAsSequenceOptions(true, false);
        return source.AsSequence(options);
    }

    internal static TResult ExecuteAsync<TSource, TResult>(this MethodInfo method, IQueryable<TSource> source, Expression? expression, CancellationToken cancellationToken = default)
    {
        if (source.Provider is not IAsyncQueryProvider provider)
        {
            throw new InvalidOperationException(CoreStrings.IQueryableProviderNotAsync);
        }

        if (method.IsGenericMethod)
        {
            MethodInfo methodInfo;
            if (method.GetGenericArguments().Length != 2)
            {
                methodInfo = method.MakeGenericMethod(typeof(TSource));
            }
            else
            {
                methodInfo = method.MakeGenericMethod(typeof(TSource), typeof(TResult).GetGenericArguments().Single<Type>());
            }

            method = methodInfo;
        }

        Expression[] expressionArray;
        if (expression != null)
        {
            expressionArray = [source.Expression, expression];
        }
        else
        {
            expressionArray = [source.Expression];
        }

        Expression callExpression = Expression.Call(null, method, expressionArray);
        return provider.ExecuteAsync<TResult>(callExpression, cancellationToken);
    }
}

internal sealed class SingleChecker
{
    /// <summary>
    /// running const mark
    /// </summary>
    private const int Running = 1;

    /// <summary>
    /// not running const mark
    /// </summary>
    private const int Unrunning = 0;
    /// <summary>
    /// run status
    /// </summary>
    private int _runStatus;

    public SingleChecker()
    {
        _runStatus = Unrunning;
    }
    public bool Start()
    {
        return Interlocked.CompareExchange(ref _runStatus, Running, Unrunning) == Unrunning;
    }

    public bool IsRunning()
    {
        return _runStatus == Running;
    }

    /// <summary>
    /// Stop Check
    /// </summary>
    /// <param name="mustExchange">must exchange</param>
    public void Stop(bool mustExchange = false)
    {
        if (Interlocked.Exchange(ref _runStatus, Unrunning) != Running && !mustExchange)
        {
            throw new ShardingException("one by one stop error,current is not running");
        }
    }
}

internal static class TaskHelper
{
    public static Task<T[]> WhenAllFastFail<T>(params Task<T>[] tasks)
    {
        if (tasks.IsNull())
        {
            return Task.FromResult(Array.Empty<T>());
        }
        // defensive copy.
        var defensive = (tasks.Clone() as Task<T>[])!;

        var tcs = new TaskCompletionSource<T[]>();
        var remaining = defensive.Length;

        void Check(Task t)
        {
            switch (t.Status)
            {
                case TaskStatus.Faulted:
                    // we 'try' as some other task may beat us to the punch.
                    tcs.TrySetException(t.Exception!.InnerException!);
                    break;
                case TaskStatus.Canceled:
                    // we 'try' as some other task may beat us to the punch.
                    tcs.TrySetCanceled();
                    break;
                default:

                    // we can safely set here as no other task remains to run.
                    if (Interlocked.Decrement(ref remaining) == 0)
                    {
                        // get the results into an array.
                        var results = new T[defensive.Length];
                        for (var i = 0; i < tasks.Length; ++i)
                        {
                            results[i] = defensive[i].Result;
                        }

                        tcs.SetResult(results);
                    }

                    break;
            }
        }

        foreach (var task in defensive)
        {
            task.ContinueWith(Check, default, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        return tcs.Task;
    }
}

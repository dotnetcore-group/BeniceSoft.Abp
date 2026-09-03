using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IShardingTrackingExecutor
{
    T Execute<T>(IQueryCompilerContext context);

    T ExecuteAsync<T>(IQueryCompilerContext context, CancellationToken cancellationToken = default);
}

internal sealed class ShardingTrackingExecutor(IShardingQueryExecutor executor, INativeTrackingExecutor native, ITrackerManager trackerManager) : IShardingTrackingExecutor
{
    //对象查询追踪方法
    private static readonly MethodInfo TrackMethod = typeof(NativeTrackingExecutor).GetMethod(nameof(NativeTrackingExecutor.Track), BindingFlags.Instance | BindingFlags.Public)!;

    //对象查询追踪方法
    private static readonly MethodInfo TrackAsyncMethod = typeof(NativeTrackingExecutor).GetMethod(nameof(NativeTrackingExecutor.TrackAsync), BindingFlags.Instance | BindingFlags.Public)!;

    //列表查询追踪方法
    private static readonly MethodInfo TrackListMethod = typeof(NativeTrackingExecutor).GetMethod(nameof(NativeTrackingExecutor.TrackList), BindingFlags.Instance | BindingFlags.Public)!;

    //列表查询追踪方法
    private static readonly MethodInfo TrackListAsyncMethod = typeof(NativeTrackingExecutor).GetMethod(nameof(NativeTrackingExecutor.TrackListAsync), BindingFlags.Instance | BindingFlags.Public)!;

    public T Execute<T>(IQueryCompilerContext context)
    {
        var compilerExecutor = context.Executor;
        if (compilerExecutor == null)
        {
            if (context is IMergeQueryCompilerContext mergeQueryCompilerContext)
            {
                return executor.Execute<T>(mergeQueryCompilerContext);
            }

            throw new ShardingNotFoundException(context.Expression.Print());
        }

        //native query
        var result = compilerExecutor.GetQueryCompiler().Execute<T>(compilerExecutor.GetExpression());
        //native query track
        return Execute(result, context, TrackListMethod, TrackMethod);

    }

    private T Execute<T>(T result, IQueryCompilerContext context, MethodInfo enumerableMethod, MethodInfo entityMethod)
    {
        //native query
        if (context.IsParallel && context.NoTracking)
        {
            Type type;

            if (context.IsEnumerable)
            {
                type = context.Expression.Type.GetGenericArguments()[0];
            }
            else
            {
                type = (context.Expression as MethodCallExpression)!.GetEntityType();
            }

            if (trackerManager.UseTrack(type))
            {
                if (context.IsEnumerable)
                {
                    return Execute(enumerableMethod, context, type, result);
                }
                else if (context.IsEntityQuery())
                {
                    return Execute(entityMethod, context, type, result);
                }
            }

            return result;
        }

        return result;
    }

    private T Execute<T>(MethodInfo executorMethod, IQueryCompilerContext context, Type queryEntityType, T result)
    {
        return (T)executorMethod.MakeGenericMethod(queryEntityType).Invoke(native, [context, result])!;
    }

    public T ExecuteAsync<T>(IQueryCompilerContext context, CancellationToken cancellationToken = default)
    {
        var compilerExecutor = context.Executor;
        if (compilerExecutor == null)
        {
            if (context is IMergeQueryCompilerContext mergeContext)
            {
                return executor.ExecuteAsync<T>(mergeContext, cancellationToken);
            }

            throw new ShardingNotFoundException(context.Expression.Print());
        }

        //native query
        var result = compilerExecutor.GetQueryCompiler().ExecuteAsync<T>(compilerExecutor.GetExpression(), cancellationToken);

        //native query track
        return Execute(result, context, TrackListAsyncMethod, TrackAsyncMethod);
    }
}

internal interface INativeTrackingExecutor
{
    T Track<T>(IQueryCompilerContext context, T result);

    Task<T> TrackAsync<T>(IQueryCompilerContext context, Task<T> task);

    IEnumerable<T> TrackList<T>(IQueryCompilerContext context, IEnumerable<T> enumerable);

    IAsyncEnumerable<T> TrackListAsync<T>(IQueryCompilerContext context, IAsyncEnumerable<T> enumerable);
}

internal sealed class NativeTrackingExecutor(IQueryTracker queryTracker, ITrackerManager trackerManager) : INativeTrackingExecutor
{
    public T Track<T>(IQueryCompilerContext context, T result)
    {

        if (result != null)
        {

            if (trackerManager.UseTrack(result.GetType()))
            {
                var trackedEntity = queryTracker.Track(result, context.DbContext);
                if (trackedEntity != null)
                {
                    return (T)trackedEntity;
                }
            }
        }

        return result;
    }

    public async Task<T> TrackAsync<T>(IQueryCompilerContext context, Task<T> task)
    {
        var result = await task;
        return Track(context, result);
    }

    public IEnumerable<T> TrackList<T>(IQueryCompilerContext context, IEnumerable<T> enumerable)
    {
        return new QueryTrackerEnumerable<T>(context.DbContext, enumerable);
    }

    public IAsyncEnumerable<T> TrackListAsync<T>(IQueryCompilerContext context, IAsyncEnumerable<T> enumerable)
    {

        return new QueryTrackerAsyncEnumerable<T>(context.DbContext, enumerable);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IShardingQueryExecutor
{
    T Execute<T>(IMergeQueryCompilerContext context);

    T ExecuteAsync<T>(IMergeQueryCompilerContext context, CancellationToken cancellationToken = default);
}

internal sealed class ShardingQueryExecutor(IStreamMergeContextFactory factory) : IShardingQueryExecutor
{
    public T Execute<T>(IMergeQueryCompilerContext context)
    {
        //如果根表达式为tolist toarray getenumerator等表示需要迭代
        if (context.IsEnumerable)
        {
            return ExecuteCore<T>(context);
        }

        return ExecuteCore<T>(context, false, default);
    }

    public T ExecuteAsync<T>(IMergeQueryCompilerContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.IsEnumerable)
        {
            return ExecuteCore<T>(context);
        }

        if (typeof(T).HasImplemented(typeof(Task<>)))
        {
            return ExecuteCore<T>(context, true, cancellationToken);
        }

        throw new ShardingException($"db context operator not support query expression:[{context.Expression.Print()}] result type:[{typeof(T).FullName}]");
    }

    private T ExecuteCore<T>(IMergeQueryCompilerContext context, bool async, CancellationToken cancellationToken = default)
    {
        var name = context.Name;
        switch (name)
        {
            case nameof(Enumerable.First):
                return Execute<T>(typeof(FirstEnsureMerge<>), context, async, cancellationToken);
            case nameof(Enumerable.FirstOrDefault):
                return Execute<T>(typeof(FirstOrDefaultEnsureMerge<>), context, async, cancellationToken);
            case nameof(Enumerable.Last):
                return Execute<T>(typeof(LastEnsureMerge<>), context, async, cancellationToken);
            case nameof(Enumerable.LastOrDefault):
                return Execute<T>(typeof(LastOrDefaultEnsureMerge<>), context, async, cancellationToken);
            case nameof(Enumerable.Single):
                return Execute<T>(typeof(SingleEnsureMerge<>), context, async, cancellationToken);
            case nameof(Enumerable.SingleOrDefault):
                return Execute<T>(typeof(SingleOrDefaultEnsureMerge<>), context, async, cancellationToken);
            case nameof(Enumerable.Count):
                return Execute<T>(typeof(CountEnsureMerge<>), context, async, cancellationToken);
            case nameof(Enumerable.LongCount):
                return Execute<T>(typeof(LongCountEnsureMerge<>), context, async, cancellationToken);
            case nameof(Enumerable.Any):
                return Execute<T>(typeof(AnyEnsureMerge<>), context, async, cancellationToken);
            case nameof(Enumerable.All):
                return Execute<T>(typeof(AllEnsureMerge<>), context, async, cancellationToken);
            case nameof(Enumerable.Max):
                return Execute2<T>(typeof(MaxEnsureMerge<,>), context, async, cancellationToken);
            case nameof(Enumerable.Min):
                return Execute2<T>(typeof(MinEnsureMerge<,>), context, async, cancellationToken);
            case nameof(Enumerable.Sum):
                return Execute2<T>(typeof(SumEnsureMerge<,>), context, async, cancellationToken);
            case nameof(Enumerable.Average):
                return Execute3<T>(typeof(AverageAsyncInMemoryMergeEngine<,,>), context, async, cancellationToken);
            case nameof(Enumerable.Contains):
                return Execute<T>(typeof(ContainsEnsureMerge<>), context, async, cancellationToken);
            case nameof(EntityFrameworkQueryableExtensions.ExecuteUpdate):
                return Execute<T>(typeof(UpdateEnsureMerge<>), context, async, cancellationToken);
            case nameof(EntityFrameworkQueryableExtensions.ExecuteDelete):
                return Execute<T>(typeof(DeleteEnsureMerge<>), context, async, cancellationToken);
            default:
                break;
        }

        throw new ShardingException($"db context operator not support query expression:[{context.Expression.Print()}]  result type:[{typeof(T).FullName}]");
    }

    private StreamMergeContext GetStreamMergeContext(IMergeQueryCompilerContext context)
    {
        return factory.Create(context);
    }

    private TResult ExecuteCore<TResult>(IMergeQueryCompilerContext context)
    {
        var query = context.Result.Queryable;
        var type = query.ElementType;
        var streamMergeContext = GetStreamMergeContext(context);

        var mergeType = typeof(StreamMergeEnumerable<>).MakeGenericType(type);
        return (TResult)(Activator.CreateInstance(mergeType, streamMergeContext)
            ?? throw new ShardingException($"Unable to create merge enumerable for type [{type}]."));
    }

    private TResult Execute<TResult>(Type mergeType, IMergeQueryCompilerContext mergeContext, bool async, CancellationToken cancellationToken)
    {
        var combineQueryable = mergeContext.Result.Queryable;
        var entityType = combineQueryable.ElementType;
        var streamType = mergeType.MakeGenericType(entityType);
        var streamMergeContext = GetStreamMergeContext(mergeContext);
        var streamEngine = Activator.CreateInstance(streamType, streamMergeContext)
            ?? throw new ShardingException($"Unable to create ensure merge engine for type [{streamType}].");
        var methodName = async ? nameof(IEnsureMerge<object>.MergeAsync) : nameof(IEnsureMerge<object>.Merge);
        var streamMethod = streamType.GetMethod(methodName);
        if (streamMethod == null)
        {
            throw new ShardingException($"cant found IEnsureMerge method [{methodName}]");
        }

        var args = async ? new object[] { cancellationToken } : Array.Empty<object>();
        return (TResult)streamMethod.Invoke(streamEngine, args)!;
    }

    private TResult Execute2<TResult>(Type mergeType, IMergeQueryCompilerContext mergeContext, bool async, CancellationToken cancellationToken)
    {
        var streamMergeContext = GetStreamMergeContext(mergeContext);
        var methodCall = mergeContext.Expression as MethodCallExpression
            ?? throw new ShardingException($"Expected MethodCallExpression but got [{mergeContext.Expression?.GetType().Name}].");
        var resultType = methodCall.GetResultType();
        mergeType = mergeType.MakeGenericType(resultType, resultType);
        var streamEngine = Activator.CreateInstance(mergeType, streamMergeContext)
            ?? throw new ShardingException($"Unable to create ensure merge engine for type [{mergeType}].");
        var methodName = async ? nameof(IEnsureMerge<object>.MergeAsync) : nameof(IEnsureMerge<object>.Merge);
        var streamMethod = mergeType.GetMethod(methodName);
        if (streamMethod == null)
        {
            throw new ShardingException($"cant found IEnsureMerge method [{methodName}]");
        }

        var args = async ? new object[] { cancellationToken } : Array.Empty<object>();
        return (TResult)streamMethod.Invoke(streamEngine, args)!;
    }

    private TResult Execute3<TResult>(Type mergeType, IMergeQueryCompilerContext mergeContext, bool async, CancellationToken cancellationToken)
    {
        var combineQueryable = mergeContext.Result.Queryable;
        var entityType = combineQueryable.ElementType;
        var methodCall = mergeContext.Expression as MethodCallExpression
            ?? throw new ShardingException($"Expected MethodCallExpression but got [{mergeContext.Expression?.GetType().Name}].");
        var resultType = methodCall.GetResultType();
        if (async)
        {
            mergeType = mergeType.MakeGenericType(entityType, typeof(TResult).GetGenericArguments()[0], resultType);
        }
        else
        {
            mergeType = mergeType.MakeGenericType(entityType, typeof(TResult), resultType);
        }

        var streamMergeContext = GetStreamMergeContext(mergeContext);
        var streamEngine = Activator.CreateInstance(mergeType, streamMergeContext)
            ?? throw new ShardingException($"Unable to create ensure merge engine for type [{mergeType}].");
        var methodName = async ? nameof(IEnsureMerge<object>.MergeAsync) : nameof(IEnsureMerge<object>.Merge);
        var streamMethod = mergeType.GetMethod(methodName);
        if (streamMethod == null)
        {
            throw new ShardingException($"cant found InMemoryAsyncStreamMergeEngine method [{methodName}]");
        }

        var args = async ? new object[] { cancellationToken } : Array.Empty<object>();
        return (TResult)streamMethod.Invoke(streamEngine, args)!;
    }
}

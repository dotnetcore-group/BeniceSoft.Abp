using BeniceSoft.Core;
namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IStreamMergeContextFactory
{
    StreamMergeContext Create(IMergeQueryCompilerContext context);
}

internal sealed class StreamMergeContextFactory(IQueryableParse queryableParse, IQueryableRewrite queryableRewrite, IQueryableOptimize queryableOptimize) : IStreamMergeContextFactory
{
    public StreamMergeContext Create(IMergeQueryCompilerContext context)
    {
        var parseResult = queryableParse.Parse(context);

        var rewriteResult = queryableRewrite.Rewrite(context, parseResult);
        var optimizeResult = queryableOptimize.Optimize(context, parseResult, rewriteResult);
        CheckMergeContext(context, parseResult);
        return new StreamMergeContext(context, parseResult, rewriteResult, optimizeResult);
    }

    private void CheckMergeContext(IMergeQueryCompilerContext mergeQueryCompilerContext, IParseResult parseResult)
    {
        var pagedContext = parseResult.PagedContext;
        if (pagedContext.Skip is < 0)
        {
            throw new ShardingException($"queryable skip should >= 0");
        }

        if (pagedContext.Take is < 0)
        {
            throw new ShardingException($"queryable take should >= 0");
        }

        if (!mergeQueryCompilerContext.IsEnumerable)
        {
            if ((nameof(Enumerable.Last) == mergeQueryCompilerContext.Name || nameof(Enumerable.LastOrDefault) == mergeQueryCompilerContext.Name) && parseResult.OrderByContext.Sorts.IsNull())
            {
                throw new InvalidOperationException("Queries performing 'LastOrDefault' operation must have a deterministic sort order. Rewrite the query to apply an 'OrderBy' operation on the sequence before calling 'LastOrDefault'");
            }
        }
    }
}

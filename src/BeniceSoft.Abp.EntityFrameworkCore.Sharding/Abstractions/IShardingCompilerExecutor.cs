using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IShardingCompilerExecutor
{
    T Execute<T>(IShardingDbContext context, Expression query);

    T ExecuteAsync<T>(IShardingDbContext context, Expression query, CancellationToken cancellationToken = default);
}

/// <summary>
/// 默认的分片编译执行者
/// </summary>
internal sealed class ShardingCompilerExecutor(IShardingTrackingExecutor tracker, IQueryCompilerContextFactory factory, IPrepareParser prepareParser, IShardingRouteManager manager, ILogger<ShardingCompilerExecutor> logger) : IShardingCompilerExecutor
{
    public T Execute<T>(IShardingDbContext context, Expression query)
    {
        //预解析表达式
        var prepareParseResult = prepareParser.Parse(context, query);
        logger.LogDebug($"compile parameter:{prepareParseResult}");
        using (new ShardingQueryScope(prepareParseResult, manager))
        {
            var compilerContext = factory.Create(prepareParseResult);
            return tracker.Execute<T>(compilerContext);
        }
    }

    public T ExecuteAsync<T>(IShardingDbContext context, Expression query,
        CancellationToken cancellationToken = default)
    {
        //预解析表达式
        var prepareParseResult = prepareParser.Parse(context, query);

        logger.LogDebug($"compile parameter:{prepareParseResult}");

        using (new ShardingQueryScope(prepareParseResult, manager))
        {
            var compilerContext = factory.Create(prepareParseResult);
            return tracker.ExecuteAsync<T>(compilerContext, cancellationToken);
        }
    }
}

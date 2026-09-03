using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal sealed class QueryCompilerExecutor
{
    private readonly IQueryCompiler _queryCompiler;
    private readonly Expression _queryExpression;

    public QueryCompilerExecutor(DbContext ctx, Expression queryExpression)
    {
        _queryCompiler = ctx.GetService<IQueryCompiler>();
        var visitor = new DbContextReplaceQueryableVisitor(ctx);
        var expression = visitor.Visit(queryExpression);
        _queryExpression = expression;
    }

    public IQueryCompiler GetQueryCompiler()
    {
        return _queryCompiler;
    }

    public Expression GetExpression()
    {
        return _queryExpression;
    }
}

internal sealed class ShardingQueryCompiler(IShardingRuntimeContext context, IQueryContextFactory queryContextFactory, ICompiledQueryCache compiledQueryCache, ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator, IDatabase database, IDiagnosticsLogger<DbLoggerCategory.Query> logger, ICurrentDbContext currentContext, IEvaluatableExpressionFilter evaluatableExpressionFilter, IModel model) : QueryCompiler(queryContextFactory, compiledQueryCache, compiledQueryCacheKeyGenerator, database, logger, currentContext, evaluatableExpressionFilter, model), IShardingDbContextAvailable
{
    private readonly IShardingCompilerExecutor _executor = context.CompilerExecutor;

    public IShardingDbContext DbContext { get; } = currentContext.Context as IShardingDbContext ?? throw new ShardingException("db context operator is not IShardingDbContext");

    public override TResult Execute<TResult>(Expression query)
    {
        return _executor.Execute<TResult>(DbContext, query);
    }

    public override TResult ExecuteAsync<TResult>(Expression query, CancellationToken cancellationToken = default)
    {
        return _executor.ExecuteAsync<TResult>(DbContext, query, cancellationToken);
    }
}

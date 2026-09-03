using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal class QueryCombineResult(IQueryable queryable, IQueryCompilerContext context)
{
    public IQueryable Queryable { get; } = queryable;

    public IQueryCompilerContext Context { get; } = context;
}

internal sealed class AllQueryCombineResult(LambdaExpression? expression, IQueryable queryable, IQueryCompilerContext context) : QueryCombineResult(queryable, context)
{
    public LambdaExpression? Expression { get; } = expression;
}

internal sealed class ConstantQueryCombineResult(object? constant, IQueryable queryable, IQueryCompilerContext context) : QueryCombineResult(queryable, context)
{
    public object? Constant { get; } = constant;
}

internal sealed class UpdateQueryCombineResult(LambdaExpression? expression, IQueryable queryable, IQueryCompilerContext context) : QueryCombineResult(queryable, context)
{
    public LambdaExpression? Expression { get; } = expression;
}

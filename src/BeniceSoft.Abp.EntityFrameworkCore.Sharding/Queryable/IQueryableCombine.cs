using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IQueryableCombine
{
    QueryCombineResult Combine(IQueryCompilerContext context);
}

internal class QueryableCombine : IQueryableCombine
{
    private static bool IsEnumerableQuery(IQueryCompilerContext context)
    {
        var type = context.Expression.Type;
        return type.HasImplemented(typeof(IQueryable<>));
    }

    protected static Type GetEntityType(IQueryCompilerContext context)
    {
        if (context.IsEnumerable)
        {
            return context.Expression.Type.GetGenericArguments()[0];
        }
        else
        {
            return ((MethodCallExpression)context.Expression).GetEntityType();
        }
    }

    protected virtual QueryCombineResult GetQueryCombineResult(IQueryable queryable, Expression? secondExpression, IQueryCompilerContext context)
    {
        return new QueryCombineResult(queryable, context);
    }

    protected virtual IQueryable Combine(IQueryable queryable, Expression secondExpression, IQueryCompilerContext context)
    {
        return queryable;
    }

    public virtual QueryCombineResult Combine(IQueryCompilerContext context)
    {
        if (context.Expression is not MethodCallExpression methodCallExpression)
        {
            throw new InvalidOperationException($"{nameof(context)}'s is not {nameof(MethodCallExpression)}");
        }

        if (methodCallExpression.Arguments.Count is < 1 or > 2)
        {
            throw new ArgumentException($"argument count must 1 or 2 :[{methodCallExpression.Print()}]");
        }

        IQueryable? queryable = null;
        Expression? secondExpression = null;
        for (var i = 0; i < methodCallExpression.Arguments.Count; i++)
        {
            var expression = methodCallExpression.Arguments[i];
            if (typeof(IQueryable).IsAssignableFrom(expression.Type))
            {
                if (queryable != null)
                {
                    throw new ArgumentException($"argument found more one IQueryable :[{methodCallExpression.Print()}]");
                }

                var type = typeof(EnumerableQuery<>);
                type = type.MakeGenericType(GetEntityType(context));
                queryable = (IQueryable)(Activator.CreateInstance(type, expression)
                             ?? throw new InvalidOperationException($"Failed to create EnumerableQuery for [{type}]"));
            }
            else
            {
                secondExpression = expression;
            }
        }

        if (queryable == null)
        {
            throw new ArgumentException($"argument not found IQueryable :[{methodCallExpression}]");
        }

        if (methodCallExpression.Arguments.Count == 2)
        {
            if (secondExpression == null)
            {
                throw new ShardingInvalidOperationException(methodCallExpression.Print());
            }

            // ReSharper disable once VirtualMemberCallInConstructor
            queryable = Combine(queryable, secondExpression, context);
        }

        return GetQueryCombineResult(queryable, secondExpression, context);
    }
}

internal sealed class AllQueryableCombine : QueryableCombine
{
    protected override IQueryable Combine(IQueryable queryable, Expression secondExpression, IQueryCompilerContext context)
    {
        return queryable;
    }

    protected override QueryCombineResult GetQueryCombineResult(IQueryable queryable, Expression? secondExpression, IQueryCompilerContext context)
    {
        LambdaExpression? expression = null;
        if (secondExpression is UnaryExpression where && where.Operand is LambdaExpression lambdaExpression)
        {
            expression = lambdaExpression;
        }

        return new AllQueryCombineResult(expression, queryable, context);
    }
}

internal sealed class ConstantQueryableCombine : QueryableCombine
{
    protected override IQueryable Combine(IQueryable queryable, Expression secondExpression, IQueryCompilerContext context)
    {
        if (secondExpression is not ConstantExpression)
        {
            throw new ShardingInvalidOperationException(context.Expression.Print());
        }

        return queryable;
    }

    protected override QueryCombineResult GetQueryCombineResult(IQueryable queryable, Expression? secondExpression, IQueryCompilerContext context)
    {
        if (secondExpression is not ConstantExpression constantExpression)
        {

            throw new ShardingException($"not found constant {context.Expression.Print()}");
        }

        var constantItem = constantExpression.Value;
        return new ConstantQueryCombineResult(constantItem, queryable, context);
    }
}

internal sealed class EnumerableQueryableCombine : QueryableCombine
{
    public override QueryCombineResult Combine(IQueryCompilerContext context)
    {
        var type = typeof(EnumerableQuery<>);
        type = type.MakeGenericType(GetEntityType(context));
        var queryable = (IQueryable)(Activator.CreateInstance(type, context.Expression)
                         ?? throw new InvalidOperationException($"Failed to create EnumerableQuery for [{type}]"));
        return new QueryCombineResult(queryable, context);
    }
}

internal sealed class UpdateQueryableCombine : QueryableCombine
{
    protected override IQueryable Combine(IQueryable queryable, Expression secondExpression, IQueryCompilerContext context)
    {
        if (!(secondExpression is UnaryExpression where && where.Operand is LambdaExpression))
        {
            throw new ShardingInvalidOperationException(context.Expression.Print());
        }

        return queryable;
    }

    protected override QueryCombineResult GetQueryCombineResult(IQueryable queryable, Expression? secondExpression, IQueryCompilerContext context)
    {
        LambdaExpression? setPropertyCalls = null;
        if (secondExpression is UnaryExpression where && where.Operand is LambdaExpression lambdaExpression)
        {
            setPropertyCalls = lambdaExpression;
        }

        return new UpdateQueryCombineResult(setPropertyCalls, queryable, context);
    }
}

internal sealed class SelectQueryableCombine : QueryableCombine
{
    protected override IQueryable Combine(IQueryable queryable, Expression secondExpression, IQueryCompilerContext context)
    {
        if (secondExpression != null)
        {
            if (secondExpression is UnaryExpression unaryExpression && unaryExpression.Operand is LambdaExpression lambdaExpression)
            {
                var selectCallExpression = Expression.Call(typeof(Queryable), nameof(Queryable.Select), [queryable.ElementType, lambdaExpression.Body.Type], queryable.Expression, lambdaExpression);
                return queryable.Provider.CreateQuery(selectCallExpression);
            }

            throw new ShardingException($"expression is not selector:{context.Expression.Print()}");
        }

        return queryable;
    }
}

internal sealed class WhereQueryableCombine : QueryableCombine
{
    protected override IQueryable Combine(IQueryable queryable, Expression secondExpression, IQueryCompilerContext context)
    {
        if (secondExpression is UnaryExpression where && where.Operand is LambdaExpression lambdaExpression)
        {
            var whereCallExpression = Expression.Call(typeof(Queryable), nameof(Queryable.Where), [queryable.ElementType], queryable.Expression, lambdaExpression);

            return queryable.Provider.CreateQuery(whereCallExpression);
        }

        throw new ShardingInvalidOperationException(context.Expression.Print());
    }
}

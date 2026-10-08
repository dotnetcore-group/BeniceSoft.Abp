using BeniceSoft.Extensions.DynamicQuery;
using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Abp.Extensions.DynamicQuery.EfCore.Extensions;

public static class DynamicOrderByExtensions
{
    public static IQueryable<T> OrderByDynamic<T>(this IQueryable<T> queryable, IDynamicOrderByRequest? request)
    {
        return OrderByDynamic(queryable, request?.OrderBys);
    }

    public static IQueryable<T> OrderByDynamic<T>(this IQueryable<T> queryable, IEnumerable<DynamicOrderBy>? orderBys)
    {
        if (orderBys is null)
            return queryable;

        IQueryable<T> result = queryable;
        var first = true;
        foreach (var item in orderBys)
        {
            if (string.IsNullOrWhiteSpace(item.FieldName))
                continue;

            var selector = BuildSelector<T>(item.FieldName);
            var descending = item.Direction.Equals("desc", StringComparison.OrdinalIgnoreCase);
            string methodName;
            if (first)
            {
                methodName = descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy);
                first = false;
            }
            else
            {
                methodName = descending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy);
            }

            var call = Expression.Call(
                typeof(Queryable),
                methodName,
                [typeof(T), selector.ReturnType],
                result.Expression,
                Expression.Quote(selector));
            result = result.Provider.CreateQuery<T>(call);
        }

        return result;
    }

    private static LambdaExpression BuildSelector<T>(string fieldName)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        Expression body = parameter;
        foreach (var part in fieldName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var property = body.Type.GetProperty(
                part,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property is null)
                throw new DynamicQueryException($"Property '{fieldName}' not found on type '{typeof(T).Name}'.");

            body = Expression.Property(body, property);
        }

        return Expression.Lambda(body, parameter);
    }
}

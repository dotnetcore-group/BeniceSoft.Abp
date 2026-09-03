using BeniceSoft.Core;
using System.Collections;
using System.Linq.Expressions;

namespace BeniceSoft.Office.Excel;

internal static class InternalExtensions
{
    internal static bool IsEnumerableType(this Type type)
    {
        return typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string);
    }

    internal static string AppendName(this string? aim, string? name)
    {
        if (aim.IsNull())
        {
            return name ?? string.Empty;
        }

        if (name.IsNull())
        {
            return aim ?? string.Empty;
        }

        return string.Concat(aim, ".", name);
    }

    internal static bool CanBeExported(this Type type)
    {
        type = type.GetUnderlyingType();
        return type.IsEnum || type.IsNumeric() || type.In(typeof(string), typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(Guid));
    }

    internal static Func<T, TResult> CreateConditionalGetter<T, TResult>(this Expression<Func<T, TResult>> propertySelector)
    {
        if (propertySelector is not LambdaExpression lambdaExpression)
        {
            throw new ArgumentException($"Unsupported property selector: {propertySelector}", nameof(propertySelector));
        }

        var body = lambdaExpression.Body;
        var newBody = body;
        while (body is MemberExpression or UnaryExpression { Operand: MemberExpression })
        {
            var memberAccess = body as MemberExpression ?? (MemberExpression)((UnaryExpression)body).Operand;

            if (memberAccess.Expression is not null && !memberAccess.Expression.Type.IsValueType)
            {
                newBody = Expression.Condition(
                    Expression.Equal(memberAccess.Expression, Expression.Constant(null)),
                    Expression.Default(typeof(TResult)),
                    Expression.Convert(newBody, typeof(TResult)));
            }

            body = memberAccess.Expression!;
        }

        return Expression.Lambda<Func<T, TResult>>(newBody, propertySelector.Parameters.First()).Compile();
    }

    internal static Action<THost, TProperty> CreateConditionalSetter<THost, TProperty>(this Expression<Func<THost, TProperty>> propertySelector, bool newObjectInPath = true)
    {
        if (propertySelector is not LambdaExpression lambdaExpression)
        {
            throw new ArgumentException($"Unsupported property selector: {propertySelector}", nameof(propertySelector));
        }

        var valueType = typeof(TProperty);
        var assigningObject = valueType == typeof(object);
        var writeableBody = lambdaExpression.Body is UnaryExpression { Operand: MemberExpression writeableMember } ? writeableMember : (MemberExpression)lambdaExpression.Body;
        var newValueParam = Expression.Parameter(valueType, "v");

        // If we are assigning value in the type of object, boxing and unboxing might be performed.
        Expression rightExpression = assigningObject
            ? Expression.Condition(
                Expression.Equal(newValueParam, Expression.Constant(null)),
                Expression.Default(writeableBody.Type),
                Expression.Convert(newValueParam, writeableBody.Type))
            : Expression.Convert(newValueParam, writeableBody.Type);

        var assignExpression = Expression.Assign(writeableBody, rightExpression);
        var returnLabel = Expression.Label();
        var statements = new List<Expression> { Expression.Label(returnLabel), assignExpression };

        var innerBody = writeableBody.Expression;

        while (innerBody is MemberExpression or UnaryExpression { Operand: MemberExpression })
        {
            var memberAccess = innerBody as MemberExpression ?? (MemberExpression)((UnaryExpression)innerBody).Operand;

            if (!memberAccess.Type.IsValueType)
            {
                Expression actionWhenNull = newObjectInPath ? Expression.Assign(memberAccess, Expression.New(memberAccess.Type)) : Expression.Return(returnLabel);
                var nullCheck = Expression.IfThen(Expression.Equal(memberAccess, Expression.Constant(null)), actionWhenNull);

                statements.Add(nullCheck);
            }

            innerBody = memberAccess.Expression;
        }

        statements.Reverse();
        var newBody = Expression.Block(statements);

        return Expression.Lambda<Action<THost, TProperty>>(newBody, propertySelector.Parameters[0], newValueParam).Compile();
    }

    internal static Expression<Func<THost, TProperty>> ToSelectorLambda<THost, TProperty>(
        this string propertyPath,
        THost? host = default,
        bool pathStartsWithHostType = false)
    {
        var parts = propertyPath?.Split('.');
        if (parts is null || parts.Length == 0)
        {
            throw new ArgumentException($"Property path is invalid: {propertyPath}", nameof(propertyPath));
        }

        if (pathStartsWithHostType && parts.Length <= 1)
        {
            throw new ArgumentException($"If pathStartsWithHostType is true, at least 2 path parts required.", nameof(propertyPath));
        }

        // for 'dynamic' type, 'GetType' works better than typeof.
        var hostType = host?.GetType() ?? typeof(THost);
        var param = Expression.Parameter(typeof(THost), "x"); // T will be 'object' for dynamic
        Expression body = Expression.Convert(param, hostType); // force convert to underlying type for dynamic

        // Skip the first part if required, which is the host object type.
        for (var i = pathStartsWithHostType ? 1 : 0; i < parts.Length; i++)
        {
            var member = parts[i];
            body = Expression.PropertyOrField(body, member);
        }

        var selector = Expression.Lambda<Func<THost, TProperty>>(Expression.Convert(body, typeof(TProperty)), param);

        return selector;
    }

    internal static Func<THost, TProperty> CreateConditionalGetter<THost, TProperty>(
        this string propertyPath,
        THost? host = default,
        bool pathStartsWithHostType = false)
    {
        var selector = ToSelectorLambda<THost, TProperty>(propertyPath, host, pathStartsWithHostType);
        return selector.CreateConditionalGetter();
    }

    internal static Action<THost, TProperty> CreateConditionalSetter<THost, TProperty>(
        this string propertyPath,
        THost? host = default,
        bool pathStartsWithHostType = false,
        bool newObjectInPath = true)
    {
        var selector = ToSelectorLambda<THost, TProperty>(propertyPath, host, pathStartsWithHostType);
        return selector.CreateConditionalSetter(newObjectInPath);
    }
}

using BeniceSoft.Core;
using BeniceSoft.Core.Constants;
using BeniceSoft.Extensions.DynamicQuery;
using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Abp.Extensions.DynamicQuery.EfCore.Extensions;

public static class DynamicQueryExtensions
{
    public static IQueryable<T> DynamicQueryBy<T>(this IEnumerable<T> queryable, IDynamicQueryRequest request)
    {
        return DynamicQueryBy(queryable.AsQueryable(), request);
    }

    public static IQueryable<T> DynamicQueryBy<T>(this IList<T> queryable, IDynamicQueryRequest request)
    {
        return DynamicQueryBy(queryable.AsQueryable(), request);
    }

    /// <summary>
    /// 动态查询
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="request">查询请求</param>
    /// <param name="skipNullableCheck">是否跳过空值检查</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IQueryable<T> DynamicQueryBy<T>(this IQueryable<T> queryable, IDynamicQueryRequest request, bool skipNullableCheck = false)
    {
        return DynamicQueryBy(queryable, request, out _, skipNullableCheck);
    }

    /// <summary>
    /// 动态查询
    /// </summary>
    /// <param name="queryable"></param>
    /// <param name="request">查询请求</param>
    /// <param name="queryString">生成的表达式</param>
    /// <param name="skipNullableCheck">是否跳过空值检查</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IQueryable<T> DynamicQueryBy<T>(this IQueryable<T> queryable, IDynamicQueryRequest request, out string queryString,
        bool skipNullableCheck = false)
    {
        var expression = BuildLambdaExpression<T>(request, out queryString, skipNullableCheck);

        if (expression is null) return queryable;

        var whereCallExp = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Where),
            [queryable.ElementType],
            queryable.Expression,
            expression);

        var filteredQueryable = queryable.Provider.CreateQuery<T>(whereCallExp);

        return filteredQueryable;
    }

    private static Expression<Func<T, bool>>? BuildLambdaExpression<T>(IDynamicQueryRequest request, out string queryString, bool skipNullableCheck)
    {
        queryString = string.Empty;
        if (request.ConditionGroups is not { Count: > 0 })
            return null;

        var trueConstantExp = Expression.Constant(true);
        var parameterExp = Expression.Parameter(typeof(T), "x");

        // 最终查询的条件
        var predicateExp = Expression.Lambda<Func<T, bool>>(trueConstantExp, parameterExp);

        for (var i = 0; i < request.ConditionGroups.Count; i++)
        {
            var conditionGroup = request.ConditionGroups[i];

            // 条件组组合后的条件
            var conditionGroupExp = Expression.Lambda<Func<T, bool>>(trueConstantExp, parameterExp);

            for (var j = 0; j < conditionGroup.Conditions.Count; j++)
            {
                var condition = conditionGroup.Conditions[j];

                if (string.IsNullOrWhiteSpace(condition.FieldName)) continue;

                Expression conditionExp;

                var propertyList = condition.FieldName.Split('.');
                if (propertyList.Length > 1)
                {
                    using var propertiesEnumerator = propertyList.AsEnumerable().GetEnumerator();
                    conditionExp = BuildNestedExpression(parameterExp, propertiesEnumerator, condition, skipNullableCheck);
                }
                else
                {
                    var propertyExp = Expression.Property(parameterExp, condition.FieldName);
                    conditionExp = BuildConditionFromProperty(propertyExp, condition, skipNullableCheck);
                }

                var lambdaExp = Expression.Lambda<Func<T, bool>>(conditionExp, parameterExp);

                if (j == 0) condition.Relation = BeniceSoftRelationConstant.And;

                // 每个条件组中多个条件组合后
                conditionGroupExp = condition.Relation switch
                {
                    BeniceSoftRelationConstant.And => conditionGroupExp.And(lambdaExp),
                    BeniceSoftRelationConstant.Or => conditionGroupExp.Or(lambdaExp),
                    _ => throw new DynamicQueryException("Relation invalid")
                };
            }

            if (i == 0) conditionGroup.Relation = BeniceSoftRelationConstant.And;

            predicateExp = conditionGroup.Relation switch
            {
                BeniceSoftRelationConstant.And => predicateExp.And(conditionGroupExp),
                BeniceSoftRelationConstant.Or => predicateExp.Or(conditionGroupExp),
                _ => throw new DynamicQueryException("Relation invalid")
            };
        }

        queryString = predicateExp.ToString();
        return predicateExp;
    }

    /// <summary>
    /// 根据属性真实类型 + FieldType 构建条件
    /// </summary>
    private static Expression BuildConditionFromProperty(
        Expression propertyExp,
        DynamicQueryCondition condition,
        bool skipNullableCheck)
    {
        var clrType = Nullable.GetUnderlyingType(propertyExp.Type) ?? propertyExp.Type;
        var fieldType = BeniceSoftTypeNameConstant.Normalize(condition.FieldType);
        EnsureFieldTypeMatchesProperty(fieldType, clrType);
        return BuildOperatorExpression(propertyExp, condition, clrType, skipNullableCheck);
    }

    /// <summary>
    /// 构建嵌套表达式
    /// </summary>
    private static Expression BuildNestedExpression(
        Expression expression,
        IEnumerator<string> propertiesEnumerator,
        DynamicQueryCondition condition,
        bool skipNullableCheck)
    {
        while (propertiesEnumerator.MoveNext())
        {
            var propertyName = propertiesEnumerator.Current;
            var property = expression.Type.GetProperty(propertyName)!;
            expression = Expression.Property(expression, property);

            var propertyType = property.PropertyType;
            if (propertyType != typeof(string) && propertyType.IsCollectionType())
            {
                var elementType = propertyType.GetGenericArguments()[0];
                var predicateFuncType = typeof(Func<,>).MakeGenericType(elementType, typeof(bool));
                var parameterExp = Expression.Parameter(elementType);

                var body = BuildNestedExpression(parameterExp, propertiesEnumerator, condition, skipNullableCheck);
                var predicate = Expression.Lambda(predicateFuncType, body, parameterExp);

                var queryable = Expression.Call(typeof(Queryable), nameof(Queryable.AsQueryable), [elementType], expression);

                return Expression.Call(
                    typeof(Queryable),
                    nameof(Queryable.Any),
                    [elementType],
                    queryable,
                    predicate);
            }
        }

        return BuildConditionFromProperty(expression, condition, skipNullableCheck);
    }

    /// <summary>
    /// 构建比较表达式
    /// </summary>
    private static Expression BuildOperatorExpression(Expression propertyExp, DynamicQueryCondition condition, Type type, bool skipNullableCheck)
    {
        return condition.Operator switch
        {
            ExprOperator.Equal => Equal(type, condition.Value, propertyExp, skipNullableCheck),
            ExprOperator.NotEqual => NotEqual(type, condition.Value, propertyExp, skipNullableCheck),
            ExprOperator.GreaterThan => GreaterThan(type, condition.Value, propertyExp),
            ExprOperator.GreaterThanOrEqual => GreaterThanOrEqual(type, condition.Value, propertyExp),
            ExprOperator.LessThan => LessThan(type, condition.Value, propertyExp),
            ExprOperator.LessThanOrEqual => LessThanOrEqual(type, condition.Value, propertyExp),
            ExprOperator.StartsWith => StartsWith(condition.Value, propertyExp, skipNullableCheck),
            ExprOperator.EndsWith => EndsWith(condition.Value, propertyExp, skipNullableCheck),
            ExprOperator.Contains => Contains(condition.Value, propertyExp, skipNullableCheck),
            ExprOperator.NotContains => NotContains(condition.Value, propertyExp, skipNullableCheck),
            ExprOperator.Between => Between(type, condition.Value, propertyExp),
            ExprOperator.In => In(type, condition.Value, propertyExp, skipNullableCheck),
            ExprOperator.NotIn => NotIn(type, condition.Value, propertyExp, skipNullableCheck),
            _ => throw new DynamicQueryException($"Unknown expression operator: {condition.Operator}")
        };
    }

    #region Operator Expressions

    private static BinaryExpression Between(Type type, List<string> value, Expression propertyExp)
    {
        // 时间类型：左闭右开 [start, end)；数值等其它类型：闭区间 [start, end]
        var startRaw = value.ElementAtOrDefault(0);
        var endRaw = value.ElementAtOrDefault(1);
        var hasStart = !string.IsNullOrWhiteSpace(startRaw);
        var hasEnd = !string.IsNullOrWhiteSpace(endRaw);

        if (!hasStart && !hasEnd)
        {
            throw new DynamicQueryException("Between needs at least a start or end value.");
        }

        BinaryExpression? lowerBound = null;
        BinaryExpression? upperBound = null;
        var halfOpenUpper = IsTemporalType(type);

        if (hasStart)
        {
            var startExp = Expression.Constant(ConvertToClr(type, startRaw!), type);
            lowerBound = Expression.GreaterThanOrEqual(propertyExp, Expression.Convert(startExp, propertyExp.Type));
        }

        if (hasEnd)
        {
            var endExp = Expression.Constant(ConvertToClr(type, endRaw!), type);
            var convertedEnd = Expression.Convert(endExp, propertyExp.Type);
            upperBound = halfOpenUpper
                ? Expression.LessThan(propertyExp, convertedEnd)
                : Expression.LessThanOrEqual(propertyExp, convertedEnd);
        }

        if (lowerBound is not null && upperBound is not null)
        {
            return Expression.AndAlso(lowerBound, upperBound);
        }

        return lowerBound ?? upperBound!;
    }

    private static UnaryExpression NotContains(List<string> value, Expression propertyExp, bool skipNullableCheck)
        => Expression.Not(Contains(value, propertyExp, skipNullableCheck));

    private static BinaryExpression Contains(List<string> value, Expression propertyExp, bool skipNullableCheck)
    {
        var firstValue = EnsureFirstValueNotEmpty(value);
        var nullCheckExp = skipNullableCheck ? Expression.Constant(true) : GetNullCheckExpression(propertyExp);

        // 如果是集合 x => x.RoleIds.Contains()
        if (propertyExp.Type.IsCollectionType())
        {
            var genericType = propertyExp.Type.GetGenericArguments()[0];

            var containersMethod = ContainsMethodInfo
                .MakeGenericMethod(genericType);

            var typeConverter = TypeDescriptor.GetConverter(genericType);
            var constantExp = Expression.Constant(typeConverter.ConvertFromString(firstValue), genericType);
            var containsExp = Expression.Call(
                null,
                containersMethod,
                propertyExp,
                constantExp);

            return Expression.AndAlso(nullCheckExp, containsExp);
        }

        var valueExp = Expression.Constant(firstValue.ToLower(), typeof(string));
        Expression? toStringExp = null;
        if (propertyExp.Type.IsGuid())
        {
            toStringExp = Expression.Call(
                propertyExp,
                typeof(Guid).GetMethod(nameof(Guid.ToString), Type.EmptyTypes)!);
        }

        var containsMethod = (toStringExp ?? propertyExp).Type.GetMethod("Contains", [typeof(string)])
            ?? throw new DynamicQueryException($"Type {propertyExp.Type} not defined Contains.");

        var toLowerExp = Expression.Call(
            toStringExp ?? propertyExp,
            typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);

        return Expression.AndAlso(nullCheckExp, Expression.Call(toLowerExp, containsMethod, valueExp));
    }

    private static BinaryExpression EndsWith(List<string> value, Expression propertyExp, bool skipNullableCheck)
    {
        var firstValue = EnsureFirstValueNotEmpty(value);
        var valueExp = Expression.Constant(firstValue.ToLower(), typeof(string));
        var nullCheckExp = skipNullableCheck ? Expression.Constant(true) : GetNullCheckExpression(propertyExp);

        Expression? toStringExp = null;
        if (propertyExp.Type.IsGuid())
        {
            toStringExp = Expression.Call(
                propertyExp,
                typeof(Guid).GetMethod(nameof(Guid.ToString), Type.EmptyTypes)!);
        }

        var endsWithMethod = (toStringExp ?? propertyExp).Type.GetMethod("EndsWith", [typeof(string)])
            ?? throw new DynamicQueryException($"Type {propertyExp.Type} not defined EndsWith.");

        var toLowerExp = Expression.Call(
            toStringExp ?? propertyExp,
            typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);

        return Expression.AndAlso(nullCheckExp, Expression.Call(toLowerExp, endsWithMethod, valueExp));
    }

    private static BinaryExpression StartsWith(List<string> value, Expression propertyExp, bool skipNullableCheck)
    {
        var firstValue = EnsureFirstValueNotEmpty(value);
        var valueExp = Expression.Constant(firstValue.ToLower(), typeof(string));
        var nullCheckExp = skipNullableCheck ? Expression.Constant(true) : GetNullCheckExpression(propertyExp);

        Expression? toStringExp = null;
        if (propertyExp.Type.IsGuid())
        {
            toStringExp = Expression.Call(
                propertyExp,
                typeof(Guid).GetMethod(nameof(Guid.ToString), Type.EmptyTypes)!);
        }

        var startsWithMethod = (toStringExp ?? propertyExp).Type.GetMethod("StartsWith", [typeof(string)])
            ?? throw new DynamicQueryException($"Type {propertyExp.Type} not defined StartsWith.");

        var toLowerExp = Expression.Call(
            toStringExp ?? propertyExp,
            typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);

        return Expression.AndAlso(nullCheckExp, Expression.Call(toLowerExp, startsWithMethod, valueExp));
    }

    private static BinaryExpression LessThanOrEqual(Type type, List<string> value, Expression propertyExp)
    {
        var valueExp = GetConstantExpressions(type, value).First();

        return Expression.LessThanOrEqual(propertyExp, Expression.Convert(valueExp, propertyExp.Type));
    }

    private static BinaryExpression LessThan(Type type, List<string> value, Expression propertyExp)
    {
        var valueExp = GetConstantExpressions(type, value).First();

        return Expression.LessThan(propertyExp, Expression.Convert(valueExp, propertyExp.Type));
    }

    private static BinaryExpression GreaterThanOrEqual(Type type, List<string> value, Expression propertyExp)
    {
        var valueExp = GetConstantExpressions(type, value).First();

        return Expression.GreaterThanOrEqual(propertyExp, Expression.Convert(valueExp, propertyExp.Type));
    }

    private static BinaryExpression GreaterThan(Type type, List<string> value, Expression propertyExp)
    {
        var valueExp = GetConstantExpressions(type, value).First();

        return Expression.GreaterThan(propertyExp, Expression.Convert(valueExp, propertyExp.Type));
    }

    private static UnaryExpression NotEqual(Type type, List<string> value, Expression propertyExp, bool skipNullableCheck)
        => Expression.Not(Equal(type, value, propertyExp, skipNullableCheck));

    private static BinaryExpression Equal(Type type, List<string> value, Expression propertyExp, bool skipNullableCheck)
    {
        var valueExp = GetConstantExpressions(type, value).First();

        if (type == typeof(string))
        {
            var nullCheckExp = skipNullableCheck ? Expression.Constant(true) : GetNullCheckExpression(propertyExp);

            Expression? toStringExp = null;
            if (propertyExp.Type.IsGuid())
            {
                toStringExp = Expression.Call(
                    propertyExp,
                    typeof(Guid).GetMethod(nameof(Guid.ToString), Type.EmptyTypes)!);
            }

            var toLowerExp = Expression.Call(
                toStringExp ?? propertyExp,
                typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
            var valueToLowerExp = Expression.Call(
                valueExp,
                typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
            return Expression.AndAlso(nullCheckExp, Expression.Equal(toLowerExp, valueToLowerExp));
        }

        return Expression.Equal(propertyExp, Expression.Convert(valueExp, propertyExp.Type));
    }

    private static BinaryExpression In(Type type, List<string> value, Expression propertyExp, bool skipNullableCheck)
    {
        var values = GetConstantExpressions(type, value);
        var nullCheck = skipNullableCheck ? Expression.Constant(true) : GetNullCheckExpression(propertyExp);

        Expression containsExp;
        if (propertyExp.Type.IsCollectionType())
        {
            var genericType = propertyExp.Type.GetGenericArguments()[0];
            var containsMethod = ContainsMethodInfo.MakeGenericMethod(genericType);

            containsExp = Expression.Call(
                null,
                containsMethod,
                propertyExp,
                Expression.Convert(values[0], genericType));

            if (values.Count > 1)
            {
                var index = 1;
                while (index < values.Count)
                {
                    containsExp = Expression.Or(containsExp,
                        Expression.Call(null, containsMethod, propertyExp, Expression.Convert(values[index], genericType)));
                    index++;
                }
            }

            return Expression.AndAlso(nullCheck, containsExp);
        }

        if (values.Count > 1)
        {
            if (type == typeof(string))
            {
                Expression? toStringExp = null;
                if (propertyExp.Type.IsGuid())
                {
                    toStringExp = Expression.Call(
                        propertyExp,
                        propertyExp.Type.GetMethod(nameof(Guid.ToString), Type.EmptyTypes)!);
                }

                var toLowerExp = Expression.Call(
                    toStringExp ?? propertyExp,
                    typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);

                var valueToLowerExp = Expression.Call(
                    values[0],
                    typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
                containsExp = Expression.Equal(toLowerExp, valueToLowerExp);

                var index = 1;
                while (index < values.Count)
                {
                    valueToLowerExp = Expression.Call(
                        values[index],
                        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
                    containsExp = Expression.Or(
                        containsExp,
                        Expression.Equal(
                            Expression.Call(toStringExp ?? propertyExp, typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!),
                            valueToLowerExp));
                    index++;
                }
            }
            else
            {
                containsExp = Expression.Equal(propertyExp, Expression.Convert(values[0], propertyExp.Type));
                var index = 1;
                while (index < values.Count)
                {
                    containsExp = Expression.Or(
                        containsExp,
                        Expression.Equal(
                            propertyExp, Expression.Convert(values[index], propertyExp.Type)));
                    index++;
                }
            }
        }
        else if (type == typeof(string))
        {
            Expression? toStringExp = null;
            if (propertyExp.Type.IsGuid())
            {
                toStringExp = Expression.Call(
                    propertyExp,
                    propertyExp.Type.GetMethod(nameof(Guid.ToString), Type.EmptyTypes)!);
            }

            var toLowerExp = Expression.Call(
                toStringExp ?? propertyExp,
                typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
            var valueToLower = Expression.Call(
                values[0],
                typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
            containsExp = Expression.Equal(toLowerExp, valueToLower);
        }
        else
        {
            containsExp = Expression.Equal(propertyExp, Expression.Convert(values[0], propertyExp.Type));
        }

        return Expression.And(nullCheck, containsExp);
    }

    private static UnaryExpression NotIn(Type type, List<string> value, Expression propertyExp, bool skipNullableCheck)
        => Expression.Not(In(type, value, propertyExp, skipNullableCheck));

    #endregion

    /// <summary>
    /// 是否时间类型
    /// </summary>
    private static bool IsTemporalType(Type type)
    {
        return type == typeof(DateOnly)
               || type == typeof(TimeOnly)
               || type == typeof(DateTime)
               || type == typeof(DateTimeOffset);
    }

    /// <summary>
    /// 确保第一个值不为空
    /// </summary>
    private static string EnsureFirstValueNotEmpty(List<string> value)
    {
        var firstValue = value.FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstValue)
            ? throw new DynamicQueryException("Value can not be null or empty")
            : firstValue;
    }

    /// <summary>
    /// FieldType 语义与属性 CLR 类型匹配校验
    /// </summary>
    private static void EnsureFieldTypeMatchesProperty(string? fieldType, Type clrType)
    {
        if (string.IsNullOrEmpty(fieldType))
        {
            return;
        }

        switch (fieldType)
        {
            case BeniceSoftTypeNameConstant.Date:
                if (clrType != typeof(DateOnly) &&
                    clrType != typeof(DateTime) &&
                    clrType != typeof(DateTimeOffset))
                {
                    throw new DynamicQueryException(
                        $"FieldType 'date' only supports DateOnly/DateTime/DateTimeOffset, but property is {clrType.Name}.");
                }

                break;
            case BeniceSoftTypeNameConstant.Time:
                if (clrType != typeof(TimeOnly))
                {
                    throw new DynamicQueryException(
                        $"FieldType 'time' only supports TimeOnly, but property is {clrType.Name}.");
                }

                break;
            case BeniceSoftTypeNameConstant.DateTime:
                if (clrType != typeof(DateTime) && clrType != typeof(DateTimeOffset))
                {
                    throw new DynamicQueryException(
                        $"FieldType 'datetime' only supports DateTime/DateTimeOffset, but property is {clrType.Name}.");
                }

                break;
        }
    }

    private static List<ConstantExpression> GetConstantExpressions(Type type, List<string> value)
    {
        return value.Select(item => Expression.Constant(ConvertToClr(type, item), type)).ToList();
    }

    /// <summary>
    /// 按属性真实 CLR 类型转换
    /// </summary>
    private static object ConvertToClr(Type targetType, string raw)
    {
        if (targetType == typeof(string))
        {
            return raw;
        }

        if (targetType == typeof(Guid))
        {
            return Guid.Parse(raw);
        }

        if (targetType == typeof(DateOnly))
        {
            if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly) ||
                DateOnly.TryParse(raw, out dateOnly))
            {
                return dateOnly;
            }

            throw new DynamicQueryException($"Cannot convert '{raw}' to DateOnly.");
        }

        if (targetType == typeof(TimeOnly))
        {
            if (TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeOnly) ||
                TimeOnly.TryParse(raw, out timeOnly))
            {
                return timeOnly;
            }

            throw new DynamicQueryException($"Cannot convert '{raw}' to TimeOnly.");
        }

        if (targetType == typeof(DateTime))
        {
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime) ||
                DateTime.TryParse(raw, out dateTime))
            {
                return dateTime;
            }

            throw new DynamicQueryException($"Cannot convert '{raw}' to DateTime.");
        }

        if (targetType == typeof(DateTimeOffset))
        {
            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto) ||
                DateTimeOffset.TryParse(raw, out dto))
            {
                return dto;
            }

            throw new DynamicQueryException($"Cannot convert '{raw}' to DateTimeOffset.");
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, raw, ignoreCase: true);
        }

        var typeConverter = TypeDescriptor.GetConverter(targetType);
        if (typeConverter.CanConvertFrom(typeof(string)))
        {
            try
            {
                return typeConverter.ConvertFromInvariantString(raw)
                       ?? typeConverter.ConvertFromString(raw)
                       ?? throw new DynamicQueryException($"Cannot convert '{raw}' to {targetType.Name}.");
            }
            catch (DynamicQueryException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DynamicQueryException($"Cannot convert '{raw}' to {targetType.Name}: {ex.Message}");
            }
        }

        try
        {
            return Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture)!;
        }
        catch (Exception ex)
        {
            throw new DynamicQueryException($"Cannot convert '{raw}' to {targetType.Name}: {ex.Message}");
        }
    }

    private static Expression GetNullCheckExpression(Expression propertyExp)
    {
        var isNullable = !propertyExp.Type.IsValueType ||
                         Nullable.GetUnderlyingType(propertyExp.Type) is not null;

        return isNullable
            ? Expression.NotEqual(
                propertyExp,
                Expression.Constant(propertyExp.Type.GetDefaultValue(), propertyExp.Type))
            : Expression.Constant(true, typeof(bool));
    }

    private static readonly MethodInfo ContainsMethodInfo = typeof(Enumerable).GetMethods()
        .Where(x => x.Name == nameof(Enumerable.Contains))
        .Single(x => x.GetParameters().Length == 2);
}

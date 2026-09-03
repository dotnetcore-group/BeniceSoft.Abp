using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Core;

public static class ExprBuilder
{
    public static Expression<Func<T, bool>> True<T>()
        where T : class
    {
        return t => true;
    }

    public static Expression<Func<T, bool>> False<T>()
        where T : class
    {
        return t => false;
    }

    public static Expression<Func<T, bool>>? Null<T>()
    {
        return null;
    }

    public static Expression<Func<T, bool>> Create<T>(Expression<Func<T, object>> columnName, object propertyValue, ExprOperator eop = ExprOperator.Equal)
        where T : class
    {
        return Create<T>(columnName.GetProperty().Name, propertyValue, eop);
    }

    public static Expression<Func<T, bool>> Create<T>(string propertyName, object propertyValue, ExprOperator eop = ExprOperator.Equal)
        where T : class
    {
        if (eop == ExprOperator.None || propertyName.IsNull())
        {
            return True<T>();
        }

        Expression? exp = null;
        var p = Expression.Parameter(typeof(T), "p");
        Expression member = p;
        var properties = propertyName.Split('.');
        foreach (var property in properties)
        {
            if (property.IsNull())
            {
                continue;
            }

            // 基于上一次的 member 继续访问嵌套属性，而不是每次用根参数 p
            member = Expression.PropertyOrField(member, property);
        }

        // 创建常数表达式。若 propertyValue 为 null，应指定常数的类型为 member.Type
        Expression valueExpression;
        if (propertyValue == null)
        {
            valueExpression = Expression.Constant(null, member.Type);
        }
        else
        {
            valueExpression = Expression.Constant(propertyValue, propertyValue.GetType());
        }

        // 处理实体类型是可空, 传过来的值不是可空类型的问题
        if (member.Type.IsNullableType() && member.Type.GetUnderlyingType() == propertyValue?.GetType())
        {
            valueExpression = Expression.Convert(valueExpression, member.Type);
        }
        else if (propertyValue != null && member.Type != propertyValue.GetType() && !eop.In(ExprOperator.In, ExprOperator.NotIn))
        {
            valueExpression = Expression.Convert(valueExpression, member.Type);
        }

        // 处理枚举中大于小于的问题
        if (eop.In(ExprOperator.GreaterThan, ExprOperator.GreaterThanOrEqual, ExprOperator.LessThan, ExprOperator.LessThanOrEqual))
        {
            var memberType = member.Type.GetUnderlyingType();
            if (memberType.IsEnum)
            {
                var enumUnderlyingType = memberType.GetEnumUnderlyingType();
                if (member.Type.IsNullableType())
                {
                    enumUnderlyingType = typeof(Nullable<>).MakeGenericType(enumUnderlyingType);
                }

                member = Expression.Convert(member, enumUnderlyingType);
                valueExpression = Expression.Convert(valueExpression, enumUnderlyingType);
            }
        }

        switch (eop)
        {
            case ExprOperator.Equal:
                {
                    exp = Expression.Equal(member, valueExpression);
                    break;
                }

            case ExprOperator.NotEqual:
                {
                    exp = Expression.NotEqual(member, valueExpression);
                    break;
                }

            case ExprOperator.GreaterThan:
                {
                    exp = Expression.GreaterThan(member, valueExpression);
                    break;
                }

            case ExprOperator.GreaterThanOrEqual:
                {
                    exp = Expression.GreaterThanOrEqual(member, valueExpression);
                    break;
                }

            case ExprOperator.LessThan:
                {
                    exp = Expression.LessThan(member, valueExpression);
                    break;
                }

            case ExprOperator.LessThanOrEqual:
                {
                    exp = Expression.LessThanOrEqual(member, valueExpression);
                    break;
                }

            case ExprOperator.StartsWith:
            case ExprOperator.EndsWith:
                {
                    var name = eop.ToString();
                    var method = member.Type.GetMethod(name, [valueExpression.Type]);
                    if (method == null)
                    {
                        throw new MissingMethodException(member.Type.Name);
                    }

                    exp = Expression.Call(member, method, valueExpression);
                    break;
                }

            case ExprOperator.Contains:
                {
                    // 字符串模糊匹配：member.Contains(value)
                    var method = member.Type.GetMethod(nameof(string.Contains), [valueExpression.Type]);
                    if (method == null)
                    {
                        throw new MissingMethodException($"Type {member.Type.Name} does not have a Contains method");
                    }

                    exp = Expression.Call(member, method, valueExpression);
                    break;
                }

            case ExprOperator.NotContains:
                {
                    // 字符串模糊匹配取反：!member.Contains(value)
                    var method = member.Type.GetMethod(nameof(string.Contains), [valueExpression.Type]);
                    if (method == null)
                    {
                        throw new MissingMethodException($"Type {member.Type.Name} does not have a Contains method");
                    }

                    exp = Expression.Not(Expression.Call(member, method, valueExpression));
                    break;
                }

            case ExprOperator.Between:
                {
                    if (propertyValue is not System.Collections.IList list || list.Count < 2)
                    {
                        throw new ArgumentException("Between operator requires a list with at least 2 values");
                    }

                    var minValue = Expression.Constant(list[0], list[0]!.GetType());
                    var maxValue = Expression.Constant(list[1], list[1]!.GetType());

                    if (member.Type != minValue.Type)
                    {
                        minValue = Expression.Constant(list[0], member.Type);
                        maxValue = Expression.Constant(list[1], member.Type);
                    }

                    var belowExp = Expression.GreaterThanOrEqual(member, minValue);
                    var aboveExp = Expression.LessThanOrEqual(member, maxValue);
                    exp = Expression.AndAlso(belowExp, aboveExp);
                    break;
                }

            case ExprOperator.In:
                {
                    // 优先尝试集合实例的 Contains 方法（例如 List<T>.Contains）
                    var method = valueExpression.Type.GetMethod(nameof(Enumerable.Contains), [member.Type]);
                    if (method != null)
                    {
                        exp = Expression.Call(valueExpression, method, member);
                    }
                    else
                    {
                        // 使用 System.Linq.Enumerable.Contains<T>(IEnumerable<T>, T)
                        method = member.Type.GetEnumerableContains();
                        if (method == null)
                        {
                            throw new MissingMethodException(nameof(Enumerable.Contains));
                        }

                        exp = Expression.Call(method, valueExpression, member);
                    }

                    break;
                }

            case ExprOperator.NotIn:
                {
                    var method = valueExpression.Type.GetMethod(nameof(List<int>.Contains), [member.Type]);
                    if (method != null)
                    {
                        exp = Expression.Not(Expression.Call(valueExpression, method, member));
                    }
                    else
                    {
                        method = member.Type.GetEnumerableContains();
                        if (method == null)
                        {
                            throw new MissingMethodException(nameof(Enumerable.Contains));
                        }

                        exp = Expression.Not(Expression.Call(method, valueExpression, member));
                    }

                    break;
                }

            default:
                {
                    throw new NotImplementedException(eop.ToString());
                }
        }

        return Expression.Lambda<Func<T, bool>>(exp, p);
    }

    private static MethodInfo? GetEnumerableContains(this Type type)
    {
        var method = typeof(Enumerable).GetMethods(BindingFlags.Static | BindingFlags.Public)
            .FirstOrDefault(t => t.Name == nameof(Enumerable.Contains) && t.GetParameters().Length == 2);
        method = method?.MakeGenericMethod(type);
        return method;
    }

    public static Expression<Func<T, bool>> Generate<T>(object data)
        where T : class
    {
        return new ExprBuilder<T>().Generate(data);
    }

    public static Expression<Func<T, bool>> Generate<T>(params IEnumerable<ExprSearch> exprs)
        where T : class
    {
        return new ExprBuilder<T>().Generate(exprs);
    }

    public static Expression<Func<T, object>> CreateMember<T>(string propertyName)
    {
        var p = Expression.Parameter(typeof(T), "p");
        var member = Expression.PropertyOrField(p, propertyName);
        // 直接构造目标委托类型的 Lambda，避免中间 LambdaExpression 冗余
        var exp = Expression.Lambda<Func<T, object>>(Expression.Convert(member, typeof(object)), p);
        return exp;
    }
}
using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Core;

public static partial class ObjectExtensions
{
    public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> expression)
       where T : class
    {
        ArgumentNullException.ThrowIfNull(expression);

        return Expression.Lambda<Func<T, bool>>(Expression.Not(expression.Body), expression.Parameters);
    }

    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> expression, Expression<Func<T, bool>> expr)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(expr);

        var left = expression.Parameters[0];
        var right = expr.Parameters[0];
        var binder = new ParameterBinder(left, right);

        return Expression.Lambda<Func<T, bool>>(Expression.OrElse(binder.Visit(expression.Body), expr.Body), right);
    }

    public static Expression<Func<T, bool>> OrIf<T>(this Expression<Func<T, bool>> expression, bool condition, Expression<Func<T, bool>> expr)
        where T : class
    {
        if (condition)
        {
            return expression.Or(expr);
        }

        return expression;
    }

    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> expression, string propertyName, object propertyValue, ExprOperator eop = ExprOperator.Equal)
        where T : class
    {
        var expr = ExprBuilder.Create<T>(propertyName, propertyValue, eop);
        return expression.Or(expr);
    }

    public static Expression<Func<T, bool>> OrIf<T>(this Expression<Func<T, bool>> expression, bool condition, string propertyName, object propertyValue, ExprOperator eop = ExprOperator.Equal)
        where T : class
    {
        if (condition)
        {
            return expression.Or(propertyName, propertyValue, eop);
        }

        return expression;
    }

    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> expression, Expression<Func<T, object>> propertyName, object propertyValue, ExprOperator eop = ExprOperator.Equal)
        where T : class
    {
        return expression.Or(propertyName.GetMember().Name, propertyValue, eop);
    }

    public static Expression<Func<T, bool>> OrIf<T>(this Expression<Func<T, bool>> expression, bool condition, Expression<Func<T, object>> propertyName, object propertyValue, ExprOperator eop = ExprOperator.Equal)
        where T : class
    {
        if (condition)
        {
            return expression.Or(propertyName, propertyValue, eop);
        }

        return expression;
    }

    public static Expression<Func<T, bool>>? And<T>(this Expression<Func<T, bool>>? expression, Expression<Func<T, bool>>? expr)
        where T : class
    {
        if (expression == null || expr == null)
        {
            return expression;
        }

        var left = expression.Parameters[0];
        var right = expr.Parameters[0];
        var binder = new ParameterBinder(left, right);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(binder.Visit(expression.Body), expr.Body), right);
    }

    public static Expression<Func<T, bool>>? AndIf<T>(this Expression<Func<T, bool>>? expression, bool condition, Expression<Func<T, bool>>? expr)
        where T : class
    {
        if (condition)
        {
            return expression.And(expr);
        }

        return expression;
    }

    public static Expression<Func<T, bool>>? And<T>(this Expression<Func<T, bool>>? expression, string propertyName, object propertyValue, ExprOperator eop = ExprOperator.Equal)
        where T : class
    {
        var expr = ExprBuilder.Create<T>(propertyName, propertyValue, eop);
        return expression.And(expr);
    }

    public static Expression<Func<T, bool>>? AndIf<T>(this Expression<Func<T, bool>>? expression, bool condition, string propertyName, object propertyValue, ExprOperator eop = ExprOperator.Equal)
        where T : class
    {
        if (condition)
        {
            return expression.And(propertyName, propertyValue, eop);
        }

        return expression;
    }

    public static Expression<Func<T, bool>>? And<T>(this Expression<Func<T, bool>>? expression, Expression<Func<T, object>> propertyName, object propertyValue, ExprOperator eop = ExprOperator.Equal)
        where T : class
    {
        return expression.And(propertyName.GetMember().Name, propertyValue, eop);
    }

    public static Expression<Func<T, bool>>? AndIf<T>(this Expression<Func<T, bool>>? expression, bool condition, Expression<Func<T, object>> propertyName, object propertyValue, ExprOperator eop = ExprOperator.Equal)
        where T : class
    {
        if (condition)
        {
            return expression.And(propertyName, propertyValue, eop);
        }

        return expression;
    }

    public static PredicateTranslation<T> Translate<T>(this Expression<Func<T, bool>> expression)
        where T : class
    {
        return new(expression);
    }

    public static PropertyInfo GetProperty(this LambdaExpression lambda)
    {
        return GetMemberInternal<PropertyInfo>(lambda);
    }

    public static PropertyInfo[] GetProperties(this LambdaExpression lambda)
    {
        return GetMembersInternal<PropertyInfo>(lambda);
    }

    public static MemberInfo GetMember(this LambdaExpression lambda)
    {
        return GetMemberInternal<MemberInfo>(lambda);
    }

    public static MemberInfo[] GetMembers(this LambdaExpression lambda)
    {
        return GetMembersInternal<MemberInfo>(lambda);
    }

    private static T[] GetMembersInternal<T>(LambdaExpression lambda)
        where T : MemberInfo
    {
        if (lambda.Parameters.Count != 1)
        {
            throw new InvalidDataException($"Parameters.Count is {lambda.Parameters.Count}");
        }

        var parameterExpression = lambda.Parameters[0];
        T[]? members = null;
        if (RemoveConvert(lambda.Body) is NewExpression newExpression)
        {
            var memberInfos = newExpression.Arguments.Select(t => MatchMember<T>(parameterExpression, t)).Where(p => p != null).Cast<T>().ToArray();
            members = memberInfos.Length == newExpression.Arguments.Count ? memberInfos : [];
        }
        else
        {
            var memberPath = MatchMember<T>(parameterExpression, lambda.Body);
            members = memberPath != null ? [memberPath] : [];
        }

        if (members.IsNull())
        {
            throw new ArgumentException($"the expression '{nameof(lambda)}' is not a valid member access expression. The expression should represent a simple property or field access: 't => t.MyProperty'. When specifying multiple properties or fields, use an anonymous type: 't => new {{ t.MyProperty, t.MyField }}'.");
        }

        return members;
    }

    private static T GetMemberInternal<T>(LambdaExpression lambda)
        where T : MemberInfo
    {
        if (lambda.Parameters.Count != 1)
        {
            throw new InvalidDataException($"Parameters.Count is {lambda.Parameters.Count}");
        }

        var parameterExpression = lambda.Parameters[0];
        var memberInfo = MatchMember<T>(parameterExpression, lambda.Body);
        if (memberInfo == null)
        {
            throw new ArgumentException($"the expression '{nameof(lambda)}' is not a valid member access expression. The expression should represent a simple property or field access: 't => t.MyProperty'.");
        }

        var declaringType = memberInfo.DeclaringType;
        var parameterType = parameterExpression.Type;

        if (declaringType != null && declaringType != parameterType && declaringType.IsInterface && declaringType.IsAssignableFrom(parameterType) && memberInfo is PropertyInfo propertyInfo)
        {
            var propertyGetter = propertyInfo.GetMethod;
            var interfaceMapping = parameterType.GetTypeInfo().GetRuntimeInterfaceMap(declaringType);
            var index = interfaceMapping.InterfaceMethods.FindIndex(p => p.Equals(propertyGetter));
            var targetMethod = interfaceMapping.TargetMethods[index];
            foreach (var runtimeProperty in parameterType.GetRuntimeProperties())
            {
                if (targetMethod.Equals(runtimeProperty.GetMethod))
                {
                    return (T)(object)runtimeProperty;
                }
            }
        }

        return memberInfo;
    }

    private static T? MatchMember<T>(Expression parameterExpression, Expression memberAccessExpression)
        where T : MemberInfo
    {
        var memberInfos = new List<T>();
        var unwrappedExpression = RemoveTypeAs(RemoveConvert(memberAccessExpression));

        do
        {
            if (memberInfos.Count > 1)
            {
                return null;
            }

            var memberExpression = unwrappedExpression as MemberExpression;
            if (memberExpression?.Member is not T memberInfo)
            {
                return null;
            }

            memberInfos.Insert(0, memberInfo);

            unwrappedExpression = RemoveTypeAs(RemoveConvert(memberExpression.Expression!));
        }
        while (unwrappedExpression != parameterExpression);

        return memberInfos.Single();
    }

    private static Expression? RemoveConvert(Expression? expr)
    {
        if (expr is UnaryExpression unaryExpression && (expr.NodeType == ExpressionType.Convert || expr.NodeType == ExpressionType.ConvertChecked))
        {
            return RemoveConvert(unaryExpression.Operand);
        }

        return expr;
    }

    private static Expression? RemoveTypeAs(Expression? expr)
    {
        while (expr?.NodeType == ExpressionType.TypeAs)
        {
            expr = ((UnaryExpression)RemoveConvert(expr)!).Operand;
        }

        return expr;
    }
}

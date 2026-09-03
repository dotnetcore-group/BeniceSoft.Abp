using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BeniceSoft.Core.Reflector;

internal static class UtilsExtensions
{
    #region Base
    internal static MethodInfo GetMethod<T>(Expression<T> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (expression.Body is not MethodCallExpression methodCallExpression)
        {
            throw new InvalidCastException("Cannot be converted to MethodCallExpression");
        }

        return methodCallExpression.Method;
    }

    internal static Type[] GetParameterTypes(this MethodBase method)
    {
        return method.GetParameters().Select(x => x.ParameterType).ToArray();
    }

    internal static Type UnWrapArrayType(this TypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        if (!typeInfo.IsArray)
        {
            return typeInfo.AsType();
        }

        return typeInfo.ImplementedInterfaces.First(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>)).GenericTypeArguments[0];
    }
    #endregion

    #region MethodSignature
    private static readonly ConcurrentDictionary<TypeInfo, bool> _isTaskOfTCache = new();
    private static readonly ConcurrentDictionary<TypeInfo, bool> _isValueTaskOfTCache = new();
    private static readonly Type? _voidTaskResultType = Type.GetType("System.Threading.Tasks.VoidTaskResult", false);

    internal static MethodInfo? GetMethodBySignature(this TypeInfo typeInfo, MethodSignature signature)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        return typeInfo.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Find(m => new MethodSignature(m) == signature);
    }

    internal static MethodInfo? GetDeclaredMethodBySignature(this TypeInfo typeInfo, MethodSignature signature)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        return typeInfo.DeclaredMethods.FirstOrDefault(m => new MethodSignature(m) == signature);
    }

    internal static bool IsVisible(this TypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        if (typeInfo.IsNested)
        {
            if (!typeInfo.DeclaringType!.GetTypeInfo().IsVisible())
            {
                return false;
            }

            if (!typeInfo.IsVisible || !typeInfo.IsNestedPublic)
            {
                return false;
            }
        }
        else
        {
            if (!typeInfo.IsVisible || !typeInfo.IsPublic)
            {
                return false;
            }
        }

        if (typeInfo.IsGenericType && !typeInfo.IsGenericTypeDefinition)
        {
            foreach (var argument in typeInfo.GenericTypeArguments)
            {
                if (!argument.GetTypeInfo().IsVisible())
                {
                    return false;
                }
            }
        }

        return true;
    }

    internal static bool IsTask(this TypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        return typeInfo.AsType() == typeof(Task);
    }

    internal static bool IsTaskWithResult(this TypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        return _isTaskOfTCache.GetOrAdd(typeInfo, info => info.IsGenericType && typeof(Task).IsAssignableFrom(info));
    }

    internal static bool IsVoidTaskResult(this TypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        return typeInfo.GenericTypeArguments?.Length > 0 && typeInfo.GenericTypeArguments[0] == _voidTaskResultType;
    }

    internal static bool IsValueTask(this TypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        return typeInfo.AsType() == typeof(ValueTask);
    }

    internal static bool IsValueTaskWithResult(this TypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        return _isValueTaskOfTCache.GetOrAdd(typeInfo, info => info.IsGenericType && info.GetGenericTypeDefinition() == typeof(ValueTask<>));
    }

    internal static bool IsTupleType(this Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.IsGenericType && typeof(ITuple).IsAssignableFrom(type.GetGenericTypeDefinition());
    }
    #endregion

    #region Method And ParameterInfo
    private static readonly ConcurrentDictionary<MethodInfo, PropertyInfo> _dictionary = new();

    internal static bool IsPropertyBinding(this MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return method.GetBindingProperty() != null;
    }

    internal static PropertyInfo? GetBindingProperty(this MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return _dictionary.GetOrAdd(method, m =>
        {
            foreach (var property in m.DeclaringType!.GetTypeInfo().GetProperties())
            {
                if (property.CanRead && property.GetMethod == m)
                {
                    return property;
                }

                if (property.CanWrite && property.SetMethod == m)
                {
                    return property;
                }
            }

            return null!;
        });
    }

    internal static bool HasDefault(this ParameterInfo parameter)
    {
        // parameter.HasDefaultValue will throw a FormatException when parameter is DateTime type with default value
        return (parameter.Attributes & ParameterAttributes.HasDefault) != 0;
    }

    internal static object? DefaultSafely(this ParameterInfo parameter)
    {
        try
        {
            // parameter.DefaultValue will throw a FormatException when parameter is DateTime type with default value
            return parameter.DefaultValue;
        }
        catch
        {
            return null;
        }
    }
    #endregion
}

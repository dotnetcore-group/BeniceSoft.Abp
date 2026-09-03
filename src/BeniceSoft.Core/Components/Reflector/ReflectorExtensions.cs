using System.Reflection;

namespace BeniceSoft.Core.Reflector;

/// <summary>
/// doc to https://github.com/dotnetcore/AspectCore-Framework
/// </summary>
public static class ReflectorExtensions
{
    #region ICustomAttributeReflectorProvider
    private static readonly Attribute[] _empty = [];

    public static Attribute[] GetCustomAttributes(this ICustomAttributeReflectorProvider customAttributeReflectorProvider)
    {
        ArgumentNullException.ThrowIfNull(customAttributeReflectorProvider);

        var customAttributeReflectors = customAttributeReflectorProvider.CustomAttributeReflectors;
        var customAttributeLength = customAttributeReflectors.Length;
        if (customAttributeLength == 0)
        {
            return _empty;
        }

        var attrs = new Attribute[customAttributeLength];
        foreach (var i in customAttributeLength)
        {
            attrs[i] = customAttributeReflectors[i].Invoke();
        }

        return attrs;
    }

    public static Attribute[] GetCustomAttributes(this ICustomAttributeReflectorProvider customAttributeReflectorProvider, Type attributeType)
    {
        ArgumentNullException.ThrowIfNull(customAttributeReflectorProvider);
        ArgumentNullException.ThrowIfNull(attributeType);

        var customAttributeReflectors = customAttributeReflectorProvider.CustomAttributeReflectors;
        var customAttributeLength = customAttributeReflectors.Length;
        if (customAttributeLength == 0)
        {
            return _empty;
        }

        var checkedAttrs = new Attribute[customAttributeLength];
        var @checked = 0;
        var attrToken = attributeType.TypeHandle;
        foreach (var i in customAttributeLength)
        {
            var reflector = customAttributeReflectors[i];
            if (reflector.Tokens.Contains(attrToken))
            {
                checkedAttrs[@checked++] = reflector.Invoke();
            }
        }

        if (customAttributeLength == @checked)
        {
            return checkedAttrs;
        }

        var attrs = new Attribute[@checked];
        Array.Copy(checkedAttrs, attrs, @checked);
        return attrs;
    }

    public static TAttribute[] GetCustomAttributes<TAttribute>(this ICustomAttributeReflectorProvider customAttributeReflectorProvider)
        where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(customAttributeReflectorProvider);

        var customAttributeReflectors = customAttributeReflectorProvider.CustomAttributeReflectors;
        var customAttributeLength = customAttributeReflectors.Length;
        if (customAttributeLength == 0)
        {
            return [];
        }

        var checkedAttrs = new TAttribute[customAttributeLength];
        var @checked = 0;
        var attrToken = typeof(TAttribute).TypeHandle;
        foreach (var i in customAttributeLength)
        {
            var reflector = customAttributeReflectors[i];
            if (reflector.Tokens.Contains(attrToken))
            {
                checkedAttrs[@checked++] = (TAttribute)reflector.Invoke();
            }
        }

        if (customAttributeLength == @checked)
        {
            return checkedAttrs;
        }

        var attrs = new TAttribute[@checked];
        Array.Copy(checkedAttrs, attrs, @checked);
        return attrs;
    }

    public static Attribute? GetCustomAttribute(this ICustomAttributeReflectorProvider customAttributeReflectorProvider, Type attributeType)
    {
        return GetCustomAttribute(customAttributeReflectorProvider, attributeType, inherit: false);
    }

    public static Attribute? GetCustomAttribute(this ICustomAttributeReflectorProvider customAttributeReflectorProvider, Type attributeType, bool inherit)
    {
        ArgumentNullException.ThrowIfNull(customAttributeReflectorProvider);
        ArgumentNullException.ThrowIfNull(attributeType);

        var customAttributeReflectors = customAttributeReflectorProvider.CustomAttributeReflectors;
        var customAttributeLength = customAttributeReflectors.Length;

        // 先从直接特性中查找
        Attribute? directAttr = null;
        if (customAttributeLength > 0)
        {
            var attrToken = attributeType.TypeHandle;
            foreach (var i in customAttributeLength)
            {
                var reflector = customAttributeReflectors[i];
                if (reflector.Tokens.Contains(attrToken))
                {
                    directAttr = customAttributeReflectors[i].Invoke();
                    break;
                }
            }
        }

        // 如果找到了，或者不需要继承，直接返回
        if (directAttr != null || !inherit)
        {
            return directAttr;
        }

        // 需要继承且当前成员没有该特性，检查继承链
        if (customAttributeReflectorProvider is MemberReflector<MemberInfo> memberReflector)
        {
            var memberInfo = memberReflector.GetMemberInfo();
            return memberInfo.GetCustomAttribute(attributeType, inherit: true);
        }

        return null;
    }

    public static TAttribute? GetCustomAttribute<TAttribute>(this ICustomAttributeReflectorProvider customAttributeReflectorProvider)
       where TAttribute : Attribute
    {
        return GetCustomAttribute<TAttribute>(customAttributeReflectorProvider, inherit: false);
    }

    public static TAttribute? GetCustomAttribute<TAttribute>(this ICustomAttributeReflectorProvider customAttributeReflectorProvider, bool inherit)
       where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(customAttributeReflectorProvider);

        // 先从直接特性中查找（使用缓存的 CustomAttributeReflectors）
        var directAttr = (TAttribute?)GetCustomAttribute(customAttributeReflectorProvider, typeof(TAttribute));

        // 如果找到了，或者不需要继承，直接返回
        if (directAttr != null || !inherit)
        {
            return directAttr;
        }

        // 需要继承且当前成员没有该特性，检查继承链
        // 只有 MemberReflector 才支持继承链检查
        if (customAttributeReflectorProvider is MemberReflector<MemberInfo> memberReflector)
        {
            var memberInfo = memberReflector.GetMemberInfo();
            // 使用原生 Reflection API 检查继承链
            return memberInfo.GetCustomAttribute<TAttribute>(inherit: true);
        }

        return null;
    }

    public static bool IsDefined(this ICustomAttributeReflectorProvider customAttributeReflectorProvider, Type attributeType)
    {
        ArgumentNullException.ThrowIfNull(customAttributeReflectorProvider);
        ArgumentNullException.ThrowIfNull(attributeType);

        var customAttributeReflectors = customAttributeReflectorProvider.CustomAttributeReflectors;
        var customAttributeLength = customAttributeReflectors.Length;
        if (customAttributeLength == 0)
        {
            return false;
        }

        var attrToken = attributeType.TypeHandle;
        foreach (var i in customAttributeLength)
        {
            if (customAttributeReflectors[i].Tokens.Contains(attrToken))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsDefined<TAttribute>(this ICustomAttributeReflectorProvider customAttributeReflectorProvider)
        where TAttribute : Attribute
    {
        return IsDefined(customAttributeReflectorProvider, typeof(TAttribute));
    }
    #endregion

    #region Reflection

    public static TypeReflector GetReflector(this Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return TypeReflector.Create(type.GetTypeInfo());
    }

    public static TypeReflector GetReflector(this TypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        return TypeReflector.Create(typeInfo);
    }

    public static ConstructorReflector GetReflector(this ConstructorInfo constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);

        return ConstructorReflector.Create(constructor);
    }

    public static FieldReflector GetReflector(this FieldInfo field)
    {
        ArgumentNullException.ThrowIfNull(field);

        return FieldReflector.Create(field);
    }

    public static MethodReflector GetReflector(this MethodInfo method)
    {
        return GetReflector(method, CallRefOptions.Callvirt);
    }

    public static MethodReflector GetReflector(this MethodInfo method, CallRefOptions callOption)
    {
        ArgumentNullException.ThrowIfNull(method);

        return MethodReflector.Create(method, callOption);
    }

    public static PropertyReflector GetReflector(this PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return GetReflector(property, CallRefOptions.Callvirt);
    }

    public static PropertyReflector GetReflector(this PropertyInfo property, CallRefOptions callOption)
    {
        ArgumentNullException.ThrowIfNull(property);

        return PropertyReflector.Create(property, callOption);
    }

    public static ParameterReflector GetReflector(this ParameterInfo parameterInfo)
    {
        ArgumentNullException.ThrowIfNull(parameterInfo);

        return ParameterReflector.Create(parameterInfo);
    }
    #endregion

    #region Reflectr

    public static FieldInfo? GetFieldInfo(this FieldReflector reflector)
    {
        return reflector?.GetMemberInfo();
    }

    public static MethodInfo? GetMethodInfo(this MethodReflector reflector)
    {
        return reflector?.GetMemberInfo();
    }

    public static ConstructorInfo? GetConstructorInfo(this ConstructorReflector reflector)
    {
        return reflector?.GetMemberInfo();
    }

    public static PropertyInfo? GetPropertyInfo(this PropertyReflector reflector)
    {
        return reflector?.GetMemberInfo();
    }

    #endregion
}

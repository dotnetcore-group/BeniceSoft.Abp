using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BeniceSoft.Core.Reflector;

internal static class MethodInfoConstant
{
    internal static readonly MethodInfo GetTypeFromHandle = UtilsExtensions.GetMethod<Func<RuntimeTypeHandle, Type?>>(handle => Type.GetTypeFromHandle(handle))!;

    internal static readonly MethodInfo GetMethodFromHandle = UtilsExtensions.GetMethod<Func<RuntimeMethodHandle, RuntimeTypeHandle, MethodBase?>>((h1, h2) => MethodBase.GetMethodFromHandle(h1, h2))!;
}

internal static class ReflectorCacheUtils<TMemberInfo, TReflector>
    where TMemberInfo : notnull
{
    private static readonly ConcurrentDictionary<TMemberInfo, TReflector> _dictionary = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TReflector GetOrAdd(TMemberInfo key, Func<TMemberInfo, TReflector> factory)
    {
        return _dictionary.GetOrAdd(key, k => factory(k));
    }
}

internal static class TypeInfoUtils
{
    internal static bool AreEquivalent(TypeInfo t1, TypeInfo t2)
    {
        return t1 == t2 || t1.IsEquivalentTo(t2.AsType());
    }

    internal static bool IsNullableType(this TypeInfo type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    internal static Type GetNonNullableType(this TypeInfo type)
    {
        if (IsNullableType(type))
        {
            return type.GetGenericArguments()[0];
        }

        return type.AsType();
    }

    internal static bool IsLegalExplicitVariantDelegateConversion(TypeInfo source, TypeInfo dest)
    {
        if (!IsDelegate(source) || !IsDelegate(dest) || !source.IsGenericType || !dest.IsGenericType)
        {
            return false;
        }

        var genericDelegate = source.GetGenericTypeDefinition();

        if (dest.GetGenericTypeDefinition() != genericDelegate)
        {
            return false;
        }

        var genericParameters = genericDelegate.GetTypeInfo().GetGenericArguments();
        var sourceArguments = source.GetGenericArguments();
        var destArguments = dest.GetGenericArguments();

        for (var iParam = 0; iParam < genericParameters.Length; ++iParam)
        {
            var sourceArgument = sourceArguments[iParam].GetTypeInfo();
            var destArgument = destArguments[iParam].GetTypeInfo();

            if (AreEquivalent(sourceArgument, destArgument))
            {
                continue;
            }

            var genericParameter = genericParameters[iParam].GetTypeInfo();

            if (IsInvariant(genericParameter))
            {
                return false;
            }

            if (IsCovariant(genericParameter))
            {
                if (!HasReferenceConversion(sourceArgument, destArgument))
                {
                    return false;
                }
            }
            else if (IsContravariant(genericParameter))
            {
                if (sourceArgument.IsValueType || destArgument.IsValueType)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsDelegate(TypeInfo t)
    {
        return t.IsSubclassOf(typeof(System.MulticastDelegate));
    }

    private static bool IsInvariant(TypeInfo t)
    {
        return 0 == (t.GenericParameterAttributes & GenericParameterAttributes.VarianceMask);
    }

    private static bool IsCovariant(this TypeInfo t)
    {
        return 0 != (t.GenericParameterAttributes & GenericParameterAttributes.Covariant);
    }

    internal static bool HasReferenceConversion(TypeInfo source, TypeInfo dest)
    {
        // void -> void conversion is handled elsewhere
        // (it's an identity conversion)
        // All other void conversions are disallowed.
        if (source.AsType() == typeof(void) || dest.AsType() == typeof(void))
        {
            return false;
        }

        var nnSourceType = TypeInfoUtils.GetNonNullableType(source).GetTypeInfo();
        var nnDestType = TypeInfoUtils.GetNonNullableType(dest).GetTypeInfo();

        // Down conversion
        if (nnSourceType.IsAssignableFrom(nnDestType))
        {
            return true;
        }
        // Up conversion
        if (nnDestType.IsAssignableFrom(nnSourceType))
        {
            return true;
        }
        // Interface conversion
        if (source.IsInterface || dest.IsInterface)
        {
            return true;
        }
        // Variant delegate conversion
        if (IsLegalExplicitVariantDelegateConversion(source, dest))
        {
            return true;
        }

        // Object conversion
        if (source.AsType() == typeof(object) || dest.AsType() == typeof(object))
        {
            return true;
        }

        return false;
    }

    private static bool IsContravariant(TypeInfo t)
    {
        return 0 != (t.GenericParameterAttributes & GenericParameterAttributes.Contravariant);
    }

    internal static bool IsConvertible(this TypeInfo typeInfo)
    {
        var type = GetNonNullableType(typeInfo);
        if (typeInfo.IsEnum)
        {
            return true;
        }

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean or TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Char => true,
            _ => false,
        };
    }

    internal static bool IsUnsigned(TypeInfo typeInfo)
    {
        var type = GetNonNullableType(typeInfo);
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte or TypeCode.UInt16 or TypeCode.Char or TypeCode.UInt32 or TypeCode.UInt64 => true,
            _ => false,
        };
    }

    internal static bool IsFloatingPoint(TypeInfo typeInfo)
    {
        var type = GetNonNullableType(typeInfo);
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Single or TypeCode.Double => true,
            _ => false,
        };
    }
}

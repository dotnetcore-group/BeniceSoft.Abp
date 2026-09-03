using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace BeniceSoft.Core;

/// <summary>
/// 无需递归克隆即可安全共享的类型（不可变 / 简单值类型等）。
/// </summary>
internal static class DeepClonerSafeTypes
{
    private static readonly ConcurrentDictionary<Type, bool> KnownTypes = new();

    static DeepClonerSafeTypes()
    {
        Type?[] seeds =
        [
            typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
            typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
            typeof(char), typeof(string), typeof(bool), typeof(DateTime),
            typeof(IntPtr), typeof(UIntPtr), typeof(Guid),
            Type.GetType("System.RuntimeType"),
            Type.GetType("System.RuntimeTypeHandle"),
            StringComparer.Ordinal.GetType(),
            StringComparer.CurrentCulture.GetType()
        ];

        foreach (var type in seeds)
        {
            if (type is not null)
            {
                KnownTypes.TryAdd(type, true);
            }
        }
    }

    private static bool IsSubclassOfTypeByName(this Type? type, string typeName)
    {
        while (type is not null)
        {
            if (type.Name == typeName)
            {
                return true;
            }

            type = type.BaseType;
        }

        return false;
    }

    private static bool CanReturnSameType(Type type, HashSet<Type>? processingTypes)
    {
        if (KnownTypes.TryGetValue(type, out var isSafe))
        {
            return isSafe;
        }

        if (type.IsEnum || type.IsPointer)
        {
            KnownTypes.TryAdd(type, true);
            return true;
        }

        var fullName = type.FullName;
        if (fullName is null)
        {
            KnownTypes.TryAdd(type, false);
            return false;
        }

        if (fullName.StartsWith("System.DBNull", StringComparison.Ordinal)
            || fullName.StartsWith("System.RuntimeType", StringComparison.Ordinal))
        {
            KnownTypes.TryAdd(type, true);
            return true;
        }

        if (fullName.StartsWith("System.Reflection.", StringComparison.Ordinal)
            && Equals(type.GetTypeInfo().Assembly, typeof(PropertyInfo).GetTypeInfo().Assembly))
        {
            KnownTypes.TryAdd(type, true);
            return true;
        }

        if (type.IsSubclassOfTypeByName("CriticalFinalizerObject"))
        {
            KnownTypes.TryAdd(type, true);
            return true;
        }

        if (fullName.StartsWith("Microsoft.Extensions.DependencyInjection.", StringComparison.Ordinal)
            || fullName == "Microsoft.EntityFrameworkCore.Internal.ConcurrencyDetector")
        {
            KnownTypes.TryAdd(type, true);
            return true;
        }

        if (fullName.Contains("EqualityComparer", StringComparison.Ordinal)
            && (fullName.StartsWith("System.Collections.Generic.GenericEqualityComparer`", StringComparison.Ordinal)
                || fullName.StartsWith("System.Collections.Generic.ObjectEqualityComparer`", StringComparison.Ordinal)
                || fullName.StartsWith("System.Collections.Generic.EnumEqualityComparer`", StringComparison.Ordinal)
                || fullName.StartsWith("System.Collections.Generic.NullableEqualityComparer`", StringComparison.Ordinal)
                || fullName == "System.Collections.Generic.ByteEqualityComparer"))
        {
            KnownTypes.TryAdd(type, true);
            return true;
        }

        if (!type.IsValueType)
        {
            KnownTypes.TryAdd(type, false);
            return false;
        }

        processingTypes ??= [];
        processingTypes.Add(type);

        var fields = new List<FieldInfo>();
        for (var tp = type; tp is not null; tp = tp.BaseType)
        {
            fields.AddRange(tp.DeclaredFields(f => !f.IsStatic));
        }

        foreach (var fieldInfo in fields)
        {
            var fieldType = fieldInfo.FieldType;
            if (processingTypes.Contains(fieldType))
            {
                continue;
            }

            if (!CanReturnSameType(fieldType, processingTypes))
            {
                KnownTypes.TryAdd(type, false);
                return false;
            }
        }

        KnownTypes.TryAdd(type, true);
        return true;
    }

    public static bool CanReturnSameObject([NotNullWhen(true)] Type? type)
        => type is not null && CanReturnSameType(type, null);
}

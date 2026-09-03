using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BeniceSoft.Core.Reflector;

namespace BeniceSoft.Core;

public static class TypeUtils
{
    #region Common
    public static IEnumerable<Type> FindClasses(IEnumerable<Assembly> assemblies, Func<Type, bool>? matched = null, bool onlyConcreteClasses = true)
    {
        var result = new HashSet<Type>();
        if (assemblies.IsNull())
        {
            return result;
        }

        try
        {
            foreach (var a in assemblies)
            {
                var types = a.DefinedTypes;
                foreach (var t in types)
                {
                    if (onlyConcreteClasses && !t.IsConcrete())
                    {
                        continue;
                    }

                    if (matched?.Invoke(t) is null or true)
                    {
                        result.Add(t);
                    }
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            var msg = string.Empty;
            foreach (var e in ex.LoaderExceptions)
            {
                msg += e?.Message + Environment.NewLine;
            }

            var fail = new TypeLoadException(msg, ex);
            throw fail;
        }

        return result;
    }

    public static IEnumerable<Type> FindClassesOfType(Type assignTypeFrom, IEnumerable<Type> types, bool onlyConcreteClasses = true)
    {
        var result = new HashSet<Type>();

        if (types.IsNull())
        {
            return result;
        }

        foreach (var t in types)
        {
            if (assignTypeFrom.IsAssignableFrom(t) || assignTypeFrom.IsGenericTypeDefinition && DoesTypeImplementOpenGeneric(t, assignTypeFrom))
            {
                if (!t.IsInterface)
                {
                    if (onlyConcreteClasses)
                    {
                        if (t.IsClass && !t.IsAbstract)
                        {
                            result.Add(t);
                        }
                    }
                    else
                    {
                        result.Add(t);
                    }
                }
            }
        }

        return result;
    }

    public static IEnumerable<Type> FindClassesOfType(Type assignTypeFrom, IEnumerable<Assembly> assemblies, bool onlyConcreteClasses = true)
    {
        var result = new HashSet<Type>();
        if (assignTypeFrom == null || assemblies.IsNull())
        {
            return result;
        }

        try
        {
            foreach (var a in assemblies)
            {
                var types = a.DefinedTypes;
                FindClassesOfType(assignTypeFrom, types, onlyConcreteClasses).ForEach(t => result.Add(t));
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            var msg = string.Empty;
            foreach (var e in ex.LoaderExceptions)
            {
                msg += e?.Message + Environment.NewLine;
            }

            var fail = new TypeLoadException(msg, ex);
            throw fail;
        }

        return result;
    }

    public static IEnumerable<Type> FindClassesOfType<T>(IEnumerable<Assembly> assemblies, bool onlyConcreteClasses = true)
    {
        return FindClassesOfType(typeof(T), assemblies, onlyConcreteClasses);
    }

    public static IEnumerable<Type> FindClassesOfType(Type assignTypeFrom, IEnumerable<string> assemblyNames, bool onlyConcreteClasses = true)
    {
        return FindClassesOfType(assignTypeFrom, GetAssemblies(assemblyNames.ToArray()), onlyConcreteClasses);
    }

    public static IEnumerable<Type> FindClassesOfType<T>(IEnumerable<string> assemblyNames, bool onlyConcreteClasses = true)
    {
        return FindClassesOfType(typeof(T), assemblyNames, onlyConcreteClasses);
    }

    public static IEnumerable<Assembly> GetAssemblies(IEnumerable<string> assemblyNames)
    {
        if (assemblyNames.IsNull())
        {
            return [];
        }

        var assemblies = new HashSet<Assembly>();
        foreach (var assemblyName in assemblyNames)
        {
            if (assemblyName.IsNull())
            {
                continue;
            }

            var assembly = Assembly.Load(assemblyName);
            assemblies.Add(assembly);
        }

        return assemblies;
    }

    public static IEnumerable<Assembly> GetReferencedAssemblies(Assembly? source = null, Assembly? dest = null)
    {
        var mainAss = source ?? Assembly.GetEntryAssembly();
        if (mainAss == null)
        {
            return [];
        }
        var assemblies = new HashSet<Assembly>();
        var allAssemblies = mainAss.GetReferencedAssemblies().Select(Assembly.Load);
        foreach (var ass in allAssemblies)
        {
            if (dest == null || ass.GetReferencedAssemblies().Exists(t => Assembly.Load(t) == dest))
            {
                assemblies.Add(ass);
            }
        }

        assemblies.Add(mainAss);
        return assemblies;
    }

    public static IEnumerable<Assembly> GetLocalAssemblies(string pattern = "", bool matched = true)
    {
        var needMatch = pattern.IsNotNull();
        return GetLocalAssemblies(n =>
        {
            if (needMatch)
            {
                return !(matched ^ Regex.IsMatch(n, pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
            }

            return true;
        });
    }

    public static IEnumerable<Assembly> GetLocalAssemblies(Func<string, bool> matched)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var paths = new List<string>();
        paths.AddRange(Directory.EnumerateFiles(baseDir, "*.dll"));
        paths.AddRange(Directory.EnumerateFiles(baseDir, "*.exe"));

        foreach (var path in paths)
        {
            var assemblyName = Path.GetFileNameWithoutExtension(path);

            if (matched(assemblyName))
            {
                yield return Assembly.Load(assemblyName);
            }
        }
    }

    /// <summary>
    /// does type implement generic
    /// </summary>
    /// <param name="type"></param>
    /// <param name="openGeneric"></param>
    /// <returns></returns>
    private static bool DoesTypeImplementOpenGeneric(Type type, Type openGeneric)
    {
        try
        {
            var genericTypeDefinition = openGeneric.GetGenericTypeDefinition();
            if (genericTypeDefinition.IsInterface)
            {
                var types = type.FindInterfaces((objType, objCriteria) => true, null);
                foreach (var implementedInterface in types)
                {
                    if (!implementedInterface.IsGenericType)
                    {
                        continue;
                    }

                    var isMatch = genericTypeDefinition.IsAssignableFrom(implementedInterface.GetGenericTypeDefinition());

                    if (!isMatch)
                    {
                        continue;
                    }

                    return isMatch;
                }

                return false;
            }
            else if (type.BaseType != null)
            {
                if (!type.BaseType.IsGenericType)
                {
                    return false;
                }

                var isMatch = genericTypeDefinition.IsAssignableFrom(type.BaseType.GetGenericTypeDefinition());

                return isMatch;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public static object? CreateInstance(Type type, params object[] args)
    {
        if (!type.IsConcrete())
        {
            return null;
        }

        var argLen = args?.Length ?? 0;
        var constructors = type.GetConstructors();
        foreach (var constructor in constructors)
        {
            if (constructor.IsStatic)
            {
                continue;
            }

            var parameters = constructor.GetParameters();
            if (parameters.Length < argLen)
            {
                continue;
            }

            var parameterInstances = new List<object>();

            foreach (var i in parameters.Length)
            {
                var parameter = parameters[i];
                if (argLen <= i)
                {
                    if (!parameter.HasDefaultValue)
                    {
                        break;
                    }

                    parameterInstances.Add(parameter.DefaultValue!);
                }
                else
                {
                    var arg = args![i];
                    if (arg == null)
                    {
                        if (parameter.ParameterType.IsValueType && Nullable.GetUnderlyingType(parameter.ParameterType) == null)
                        {
                            break;
                        }

                        parameterInstances.Add(arg!);
                    }
                    else
                    {
                        if (parameter.ParameterType.IsAssignableFrom(arg.GetType()))
                        {
                            parameterInstances.Add(arg);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            if (parameterInstances.Count == parameters.Length)
            {
                return constructor.GetReflector().Invoke([..parameterInstances]);
            }
        }

        return type.GetDefaultValue();
    }

    public static T? CreateInstance<T>(Type? type, params object[] args)
    {
        return (T?)CreateInstance(type ?? typeof(T), args);
    }

    public static T? CreateInstance<T>(params object[] args)
    {
        return (T?)CreateInstance(typeof(T), args);
    }
    #endregion

    #region Extensions

    #region TypeInfo
    public static bool IsEnum(this Type type)
    {
        return type.GetTypeInfo().IsEnum;
    }

    public static bool IsValueType(this Type type)
    {
        return type.GetTypeInfo().IsValueType;
    }

    public static bool IsClass(this Type type)
    {
        return type.GetTypeInfo().IsClass;
    }

    public static bool IsAbstract(this Type type)
    {
        return type.GetTypeInfo().IsAbstract;
    }

    public static bool IsInterface(this Type type)
    {
        return type.GetTypeInfo().IsInterface;
    }

    public static bool IsArray(this Type type)
    {
        return type.GetTypeInfo().IsArray;
    }

    public static bool IsGenericType(this Type type)
    {
        return type.GetTypeInfo().IsGenericType;
    }

    public static bool IsConcrete(this Type type)
    {
        return type.GetTypeInfo().IsConcrete();
    }

    public static bool IsGuid(this Type type)
    {
        return type == typeof(Guid) || Nullable.GetUnderlyingType(type) == typeof(Guid);
    }

    public static bool IsConcrete(this TypeInfo type)
    {
        return type.IsClass && !type.IsAbstract && !type.IsInterface;
    }

    public static Type[] GetBaseTypes(this Type type)
    {
        var list = new List<Type>();

        var baseType = type.BaseType;
        while (baseType != null && baseType != typeof(object))
        {
            list.Add(baseType);
            baseType = baseType.BaseType;
        }

        return [..list];
    }

    public static bool IsCollectionType(this Type type)
    {
        return type.IsArray ||
               (type.IsGenericType &&
             type.GetGenericTypeDefinition() is { } t && (t == typeof(IEnumerable<>) ||
                                                          t == typeof(ICollection<>) ||
                                                          t == typeof(List<>) ||
                                                          t == typeof(HashSet<>)));
    }

    public static bool IsTask(this Type type) => type == typeof(Task);

    public static bool IsTaskWithResult(this Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>);

    public static bool IsValueTask(this Type type) => type == typeof(ValueTask);

    public static bool IsValueTaskWithResult(this Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>);

    public static bool IsVoidType(this Type type)
        => type == typeof(void) || type == typeof(Task) || type == typeof(ValueTask);

    public static bool IsTaskWithVoidTaskResult(this Type type)
        => type.IsGenericType && type.GenericTypeArguments.Length > 0
           && type.GenericTypeArguments[0].Name == "VoidTaskResult";

    public static object? GetDefaultValue(this Type? type)
    {
        if (type == null)
        {
            return null;
        }

        return type.GetTypeInfo().GetDefaultValue();
    }

    public static object? GetDefaultValue(this TypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        if (typeInfo.AsType() == typeof(void))
        {
            return null;
        }

        switch (Type.GetTypeCode(typeInfo.AsType()))
        {
            case TypeCode.Object:
            case TypeCode.DateTime:
                if (typeInfo.IsValueType)
                {
                    return Activator.CreateInstance(typeInfo.AsType());
                }
                else
                {
                    return null;
                }

            case TypeCode.DBNull:
            case TypeCode.Empty:
            case TypeCode.String:
                return null;

            case TypeCode.Boolean:
            case TypeCode.Char:
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
                return 0;

            case TypeCode.Int64:
            case TypeCode.UInt64:
                return 0;

            case TypeCode.Single:
                return default(float);

            case TypeCode.Double:
                return default(double);

            case TypeCode.Decimal:
                return new decimal(0);

            default:
                throw new InvalidOperationException("Code supposed to be unreachable.");
        }
    }

    public static IEnumerable<ConstructorInfo> DeclaredConstructors(this Type type, Func<ConstructorInfo, bool>? predicate = null)
    {
        return type.GetTypeInfo().DeclaredConstructors.WhereSafe(predicate)!;
    }

    public static IEnumerable<FieldInfo> DeclaredFields(this Type type, Func<FieldInfo, bool>? predicate = null)
    {
        return type.GetTypeInfo().DeclaredFields.WhereSafe(predicate)!;
    }

    public static IEnumerable<PropertyInfo> DeclaredProperties(this Type type, Func<PropertyInfo, bool>? predicate = null)
    {
        return type.GetTypeInfo().DeclaredProperties.WhereSafe(predicate)!;
    }

    public static IEnumerable<MemberInfo> DeclaredMembers(this Type type, Func<MemberInfo, bool>? predicate = null)
    {
        return type.GetTypeInfo().DeclaredMembers.WhereSafe(predicate)!;
    }

    public static IEnumerable<MethodInfo> DeclaredMethods(this Type type, Func<MethodInfo, bool>? predicate = null)
    {
        return type.GetTypeInfo().DeclaredMethods.WhereSafe(predicate)!;
    }

    public static FieldInfo? GetDeclaredField(this Type type, string name)
    {
        return type.GetTypeInfo().GetDeclaredField(name);
    }

    public static PropertyInfo? GetDeclaredProperty(this Type type, string name)
    {
        return type.GetTypeInfo().GetDeclaredProperty(name);
    }

    public static MethodInfo? GetDeclaredMethod(this Type type, string name)
    {
        return type.GetTypeInfo().GetDeclaredMethod(name);
    }

    public static IEnumerable<MethodInfo> GetDeclaredMethods(this Type type, string name, Func<MethodInfo, bool>? predicate = null)
    {
        return type.GetTypeInfo().GetDeclaredMethods(name).WhereSafe(predicate)!;
    }
    #endregion

    #region Common Type
    /// <summary>
    /// Returns true if the type is one of the built in simple types.
    /// </summary>
    public static bool IsSimpleType(this Type type, bool includeNullable = true)
    {
        if (includeNullable)
        {
            type = type.GetUnderlyingType();
        }

        if (type.IsEnum)
        {
            return true;
        }

        if (type == typeof(Guid))
        {
            return true;
        }

        var tc = Type.GetTypeCode(type);
        if (tc.In(TypeCode.Empty, TypeCode.DBNull))
        {
            return false;
        }

        if (tc == TypeCode.Object)
        {
            return type.In(typeof(TimeSpan), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly));
        }

        return true;
    }

    public static bool IsNumeric(this Type type, bool nullable = true)
    {
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Decimal or TypeCode.Double or TypeCode.Single => true,
            TypeCode.Object => nullable && type.IsNullableType() && IsNumeric(GetUnderlyingType(type)),
            _ => false,
        };
    }

    public static Type GetUnderlyingType(this Type type)
    {
        if (!type.IsValueType)
        {
            return type;
        }

        var original = Nullable.GetUnderlyingType(type);
        return original ?? type;
    }

    public static bool IsNullableType(this Type type)
    {
        return Nullable.GetUnderlyingType(type) != null;
    }

    /// <summary>
    /// https://github.com/JamesNK/Newtonsoft.Json/blob/master/Src/Newtonsoft.Json/Utilities/ReflectionUtils.cs
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static string GetTypeName(this Type type)
    {
        var fullyQualifiedTypeName = type.GetTypeInfo().AssemblyQualifiedName;

        if (string.IsNullOrEmpty(fullyQualifiedTypeName))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        // loop through the type name and filter out qualified assembly details from nested type names
        var writingAssemblyName = false;
        var skippingAssemblyDetails = false;
        foreach (var i in fullyQualifiedTypeName.Length)
        {
            var current = fullyQualifiedTypeName[i];
            switch (current)
            {
                case '[':
                    {
                        writingAssemblyName = false;
                        skippingAssemblyDetails = false;
                        builder.Append(current);
                        break;
                    }
                case ']':
                    {
                        writingAssemblyName = false;
                        skippingAssemblyDetails = false;
                        builder.Append(current);
                        break;
                    }
                case ',':
                    if (!writingAssemblyName)
                    {
                        writingAssemblyName = true;
                        builder.Append(current);
                    }
                    else
                    {
                        skippingAssemblyDetails = true;
                    }

                    break;
                default:
                    if (!skippingAssemblyDetails)
                    {
                        builder.Append(current);
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    public static bool IsDefined<T>(this Type type, bool inherit = true)
        where T : Attribute
    {
        return type.IsDefined(typeof(T), inherit);
    }

    public static bool IsDefined<T>(this MemberInfo memberInfo, bool inherit = true)
        where T : Attribute
    {
        return memberInfo.IsDefined(typeof(T), inherit);
    }

    /// <summary>
    /// https://github.com/nunit/nunit/blob/111fc6b5550f33b4fceb6ac8693c5692e99a5747/src/NUnitFramework/framework/Internal/Reflect.cs
    /// </summary>
    /// <param name="type"></param>
    /// <param name="name"></param>
    /// <param name="bindingFlags"></param>
    /// <returns></returns>
    public static PropertyInfo? GetShadowingProperty(this Type type, string name, BindingFlags bindingFlags)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNullOrEmpty(name);

        if ((bindingFlags & BindingFlags.DeclaredOnly) != 0)
        {
            // If you're asking us to search a hierarchy but only want properties declared in the given type,
            // you're in the wrong place but okay:
            return type.GetProperty(name, bindingFlags);
        }

        if ((bindingFlags & (BindingFlags.Public | BindingFlags.NonPublic)) == (BindingFlags.Public | BindingFlags.NonPublic))
        {
            // If we're searching for both public and nonpublic properties, search for only public first
            // because chances are if there is a public property, it would be very surprising to detect the private shadowing property.

            for (var publicSearchType = type; publicSearchType != null; publicSearchType = publicSearchType.GetTypeInfo().BaseType)
            {
                var property = publicSearchType.GetProperty(name, (bindingFlags | BindingFlags.DeclaredOnly) & ~BindingFlags.NonPublic);
                if (property != null)
                {
                    return property;
                }
            }

            // There is no public property, so may as well not ask to include them during the second search.
            bindingFlags &= ~BindingFlags.Public;
        }

        for (var searchType = type; searchType != null; searchType = searchType.GetTypeInfo().BaseType)
        {
            var property = searchType.GetProperty(name, bindingFlags | BindingFlags.DeclaredOnly);
            if (property != null)
            {
                return property;
            }
        }

        return null;
    }
    #endregion

    #region TypeNameHelper
    private const char DefaultNestedTypeDelimiter = '+';

    private static readonly Dictionary<Type, string> _builtInTypeNames = new()
    {
        { typeof(void), "void" },
        { typeof(bool), "bool" },
        { typeof(byte), "byte" },
        { typeof(char), "char" },
        { typeof(decimal), "decimal" },
        { typeof(double), "double" },
        { typeof(float), "float" },
        { typeof(int), "int" },
        { typeof(long), "long" },
        { typeof(object), "object" },
        { typeof(sbyte), "sbyte" },
        { typeof(short), "short" },
        { typeof(string), "string" },
        { typeof(uint), "uint" },
        { typeof(ulong), "ulong" },
        { typeof(ushort), "ushort" }
    };

    /// <summary>
    /// Pretty print a type name.
    /// </summary>
    /// <param name="type">The <see cref="Type"/>.</param>
    /// <param name="fullName"><c>true</c> to print a fully qualified name.</param>
    /// <param name="includeGenericParameterNames"><c>true</c> to include generic parameter names.</param>
    /// <param name="includeGenericParameters"><c>true</c> to include generic parameters.</param>
    /// <param name="nestedTypeDelimiter">Character to use as a delimiter in nested type names</param>
    /// <returns>The pretty printed type name.</returns>
    public static string GetDisplayName(this Type type, bool fullName = true, bool includeGenericParameterNames = false, bool includeGenericParameters = false, char nestedTypeDelimiter = '.')
    {
        var builder = new StringBuilder();
        ProcessType(builder, type, fullName, includeGenericParameterNames, includeGenericParameters, nestedTypeDelimiter);
        return builder.ToString();
    }

    private static void ProcessType(StringBuilder builder, Type type, bool fullName = true, bool includeGenericParameterNames = false, bool includeGenericParameters = true, char nestedTypeDelimiter = DefaultNestedTypeDelimiter)
    {
        if (type.IsGenericType)
        {
            var genericArguments = type.GetGenericArguments();
            ProcessGenericType(builder, type, genericArguments, genericArguments.Length, fullName, includeGenericParameterNames, includeGenericParameters, nestedTypeDelimiter);
        }
        else if (type.IsArray)
        {
            ProcessArrayType(builder, type, fullName, includeGenericParameterNames, includeGenericParameters, nestedTypeDelimiter);
        }
        else if (_builtInTypeNames.TryGetValue(type, out var builtInName))
        {
            builder.Append(builtInName);
        }
        else if (type.IsGenericParameter)
        {
            if (includeGenericParameterNames)
            {
                builder.Append(type.Name);
            }
        }
        else
        {
            var name = fullName ? type.FullName! : type.Name;
            builder.Append(name);

            if (nestedTypeDelimiter != DefaultNestedTypeDelimiter)
            {
                builder.Replace(DefaultNestedTypeDelimiter, nestedTypeDelimiter, builder.Length - name.Length, name.Length);
            }
        }
    }

    private static void ProcessArrayType(StringBuilder builder, Type type, bool fullName = true, bool includeGenericParameterNames = false, bool includeGenericParameters = true, char nestedTypeDelimiter = DefaultNestedTypeDelimiter)
    {
        var innerType = type;
        while (innerType.IsArray)
        {
            innerType = innerType.GetElementType()!;
        }

        ProcessType(builder, innerType, fullName, includeGenericParameterNames, includeGenericParameters, nestedTypeDelimiter);

        while (type.IsArray)
        {
            builder.Append('[');
            builder.Append(',', type.GetArrayRank() - 1);
            builder.Append(']');
            type = type.GetElementType()!;
        }
    }

    private static void ProcessGenericType(StringBuilder builder, Type type, Type[] genericArguments, int length, bool fullName = true, bool includeGenericParameterNames = false, bool includeGenericParameters = true, char nestedTypeDelimiter = DefaultNestedTypeDelimiter)
    {
        var offset = 0;
        if (type.IsNested)
        {
            offset = type.DeclaringType!.GetGenericArguments().Length;
        }

        if (fullName)
        {
            if (type.IsNested)
            {
                ProcessGenericType(builder, type.DeclaringType!, genericArguments, offset, fullName, includeGenericParameterNames, includeGenericParameters, nestedTypeDelimiter);
                builder.Append(nestedTypeDelimiter);
            }
            else if (type.Namespace?.IsNotEmpty() == true)
            {
                builder.Append(type.Namespace);
                builder.Append('.');
            }
        }

        var genericPartIndex = type.Name.IndexOf('`');
        if (genericPartIndex <= 0)
        {
            builder.Append(type.Name);
            return;
        }

        builder.Append(type.Name, 0, genericPartIndex);

        if (includeGenericParameters)
        {
            builder.Append('<');
            for (var i = offset; i < length; i++)
            {
                ProcessType(builder, genericArguments[i], fullName, includeGenericParameterNames, includeGenericParameters, nestedTypeDelimiter);
                if (i + 1 == length)
                {
                    continue;
                }

                builder.Append(',');
                if (includeGenericParameterNames || !genericArguments[i + 1].IsGenericParameter)
                {
                    builder.Append(' ');
                }
            }

            builder.Append('>');
        }
    }
    #endregion

    #endregion
}

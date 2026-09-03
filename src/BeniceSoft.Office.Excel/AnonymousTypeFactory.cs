using BeniceSoft.Core;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;

namespace BeniceSoft.Office.Excel;

internal static class AnonymousTypeFactory
{
    private static readonly ModuleBuilder _moduleBuilder;
    private static readonly Lock _locker = new();

    static AnonymousTypeFactory()
    {
        var assemblyName = new AssemblyName { Name = "BeniceSoft.Office.Excel.AnonymousTypes" };

        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        _moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name!);
    }

    public static Type CreateType(IEnumerable<KeyValuePair<string, Type>> typePairs, bool isMutable = false, Type? parent = null)
    {
        ArgumentNullException.ThrowIfNull(typePairs);

        var keyValuePairs = typePairs as KeyValuePair<string, Type>[] ?? typePairs.ToArray();
        var propertyNames = keyValuePairs.Select(pair => pair.Key);
        var genericTypeDefinition = GetOrCreateGenericTypeDefinition(propertyNames.ToList(), isMutable, parent);

        var propertyTypes = keyValuePairs.Select(pair => pair.Value);
        return genericTypeDefinition.MakeGenericType(propertyTypes.ToArray());
    }

    private static Type GetOrCreateGenericTypeDefinition(List<string> propertyNames, bool isMutable, Type? parent)
    {
        if (propertyNames.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(propertyNames), propertyNames.Count, "At least one property name is required to create an anonymous type");
        }

        if (parent != null && !parent.GetConstructors().Exists(c => c.GetParameters().Length == 0))
        {
            throw new ArgumentException($"Parent type \"{parent.FullName}\" is not supported because it does not have a default constructor");
        }

        var genericTypeDefinitionName = GenerateGenericTypeDefinitionName(propertyNames, isMutable, parent);

        // We need to check for the type and define/create it as one atomic operation, 
        // otherwise we could get a TypeBuilder back instead of a full Type.
        Type? genericTypeDefinition;
        lock (_locker)
        {
            genericTypeDefinition = _moduleBuilder.GetType(genericTypeDefinitionName);
            if (genericTypeDefinition == null)
            {
                genericTypeDefinition = CreateGenericTypeDefinitionNoLock(genericTypeDefinitionName, propertyNames, isMutable, parent);
            }
        }

        return genericTypeDefinition!;
    }

    private static string GenerateGenericTypeDefinitionName(List<string> propertyNames, bool isMutable, Type? parent)
    {
        var keyJsonBuilder = new StringBuilder();
        keyJsonBuilder.Append('{');
        keyJsonBuilder.Append("properties=[");
        keyJsonBuilder.Append(propertyNames.Select(n => '"' + n.Replace("\"", "\"\"") + '"').JoinStr(","));
        keyJsonBuilder.Append(']');
        if (isMutable)
        {
            keyJsonBuilder.Append(",isMutable=true");
        }

        if (parent != null)
        {
            keyJsonBuilder.Append(",parent=\"");
            keyJsonBuilder.Append(parent.FullName);
            keyJsonBuilder.Append('"');
        }

        keyJsonBuilder.Append('}');

        var hashString = SHA1.Create().HashHex(keyJsonBuilder.ToString());

        var genericTypeDefinitionName = $"<>f__MyAnonymousType{hashString}`{propertyNames.Count}";
        return genericTypeDefinitionName;
    }

    private static TypeInfo CreateGenericTypeDefinitionNoLock(string genericTypeDefinitionName, ICollection<string> propertyNames, bool isMutable, Type? parent)
    {
        var typeBuilder = _moduleBuilder.DefineType(genericTypeDefinitionName, TypeAttributes.Public | TypeAttributes.AutoLayout | TypeAttributes.AnsiClass | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit, parent);
        var typeParameterNames = propertyNames.Select(propertyName => $"<{propertyName}>j__TPar").ToArray();
        var typeParameters = typeBuilder.DefineGenericParameters(typeParameterNames);

        var typeParameterPairs = propertyNames.Zip(typeParameters, (propertyName, typeParameter) => new KeyValuePair<string, GenericTypeParameterBuilder>(propertyName, typeParameter)).ToArray();

        var fieldBuilders = new List<FieldBuilder>(typeParameterPairs.Length);
        foreach (var pair in typeParameterPairs)
        {
            var propertyName = pair.Key;
            var typeParameter = pair.Value;
            var fieldAttributes = FieldAttributes.Private;
            if (!isMutable)
            {
                fieldAttributes |= FieldAttributes.InitOnly;
            }

            var fieldBuilder = typeBuilder.DefineField($"<{propertyName}>i__Field", typeParameter, fieldAttributes);
            fieldBuilders.Add(fieldBuilder);
            var property = typeBuilder.DefineProperty(propertyName, PropertyAttributes.None, typeParameter, Type.EmptyTypes);

            var getMethodBuilder = typeBuilder.DefineMethod($"get_{propertyName}", MethodAttributes.PrivateScope | MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, CallingConventions.Standard | CallingConventions.HasThis, typeParameter, Type.EmptyTypes);
            var getMethodIlGenerator = getMethodBuilder.GetILGenerator();
            getMethodIlGenerator.Emit(OpCodes.Ldarg_0);
            getMethodIlGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
            getMethodIlGenerator.Emit(OpCodes.Ret);
            property.SetGetMethod(getMethodBuilder);

            if (isMutable)
            {
                var setMethodBuilder = typeBuilder.DefineMethod($"set_{propertyName}", MethodAttributes.PrivateScope | MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, CallingConventions.Standard | CallingConventions.HasThis, null, [typeParameter]);
                var setMethodIlGenerator = setMethodBuilder.GetILGenerator();
                setMethodIlGenerator.Emit(OpCodes.Ldarg_0);
                setMethodIlGenerator.Emit(OpCodes.Ldarg_1);
                setMethodIlGenerator.Emit(OpCodes.Stfld, fieldBuilder);
                setMethodIlGenerator.Emit(OpCodes.Ret);
                property.SetSetMethod(setMethodBuilder);
            }
        }

        var defaultConstructor = parent?.GetConstructors().Find(c => c.GetParameters().Length == 0);
        DefineDefaultConstructor(typeBuilder, defaultConstructor);
        DefineEqualsMethod(typeBuilder, fieldBuilders);
        DefineGetHashCodeMethod(typeBuilder, fieldBuilders);

        var fieldPairs = propertyNames.Zip(fieldBuilders, (propertyName, fieldBuilder) => new KeyValuePair<string, FieldBuilder>(propertyName, fieldBuilder)).ToArray();
        DefineToStringMethod(typeBuilder, fieldPairs);

        return typeBuilder.CreateTypeInfo()!;
    }

    private static void DefineDefaultConstructor(TypeBuilder typeBuilder, ConstructorInfo? baseConstructor = null)
    {
        var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.PrivateScope | MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, CallingConventions.Standard | CallingConventions.HasThis, Type.EmptyTypes);

        var constructorIlGenerator = constructorBuilder.GetILGenerator();
        constructorIlGenerator.Emit(OpCodes.Ldarg_0);
        constructorIlGenerator.Emit(OpCodes.Call, typeof(object).GetConstructors().Single());

        if (baseConstructor != null)
        {
            constructorIlGenerator.Emit(OpCodes.Ldarg_0);
            constructorIlGenerator.Emit(OpCodes.Call, baseConstructor);
        }

        constructorIlGenerator.Emit(OpCodes.Ret);
    }

    private static void DefineEqualsMethod(TypeBuilder typeBuilder, List<FieldBuilder> fields)
    {
        var equalsMethodBuilder = typeBuilder.DefineMethod("Equals", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final, typeof(bool), [typeof(object)]);
        equalsMethodBuilder.DefineParameter(1, ParameterAttributes.None, "value");

        var il = equalsMethodBuilder.GetILGenerator();

        il.DeclareLocal(typeBuilder);
        il.DeclareLocal(typeof(bool));

        var label1 = il.DefineLabel();
        var label2 = il.DefineLabel();
        var label3 = il.DefineLabel();

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Isinst, typeBuilder);
        il.Emit(OpCodes.Stloc_0);
        il.Emit(OpCodes.Ldloc_0);

        // Only the last five fields can use the short form of the branch.
        const int maximumShortBranchFieldCount = 5;
        var shortBranchThreshold = Math.Max(fields.Count - maximumShortBranchFieldCount, 0);

        var currentFieldIndex = 0;
        foreach (var field in fields)
        {
            var equalityComparerGenericTypeDefinition = typeof(EqualityComparer<>);
            var equalityComparerEqualsGenericMethodDefinition = equalityComparerGenericTypeDefinition.GetMethods().Single(m => m.Name == "Equals" && m.GetParameters().Length == 2);
            var equalityComparerDefaultGenericPropertyGetterDefinition = equalityComparerGenericTypeDefinition.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!.GetGetMethod()!;

            var equalityComparerType = equalityComparerGenericTypeDefinition.MakeGenericType(field.FieldType);
            var equalityComparerEqualsMethod = TypeBuilder.GetMethod(equalityComparerType, equalityComparerEqualsGenericMethodDefinition);
            var equalityComparerDefaultPropertyGetter = TypeBuilder.GetMethod(equalityComparerType, equalityComparerDefaultGenericPropertyGetterDefinition);

            if (currentFieldIndex >= shortBranchThreshold)
            {
                il.Emit(OpCodes.Brfalse_S, label1);
            }
            else
            {
                il.Emit(OpCodes.Brfalse, label1);
            }

            il.EmitCall(OpCodes.Call, equalityComparerDefaultPropertyGetter, null);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ldfld, field);
            il.EmitCall(OpCodes.Callvirt, equalityComparerEqualsMethod, null);

            currentFieldIndex++;
        }

        il.Emit(OpCodes.Br_S, label2);
        il.MarkLabel(label1);
        il.Emit(OpCodes.Ldc_I4_0);
        il.MarkLabel(label2);
        il.Emit(OpCodes.Nop);
        il.Emit(OpCodes.Stloc_1);
        il.Emit(OpCodes.Br_S, label3);
        il.MarkLabel(label3);
        il.Emit(OpCodes.Ldloc_1);
        il.Emit(OpCodes.Ret);

        typeBuilder.DefineMethodOverride(equalsMethodBuilder, typeof(object).GetMethod("Equals", [typeof(object)])!);
    }

    private static void DefineGetHashCodeMethod(TypeBuilder typeBuilder, IEnumerable<FieldBuilder> fields)
    {
        var getHashCodeMethodBuilder = typeBuilder.DefineMethod("GetHashCode", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final, typeof(int), Type.EmptyTypes);

        var il = getHashCodeMethodBuilder.GetILGenerator();

        il.DeclareLocal(typeof(int));
        il.DeclareLocal(typeof(int));

        var hashSeed = 0;
        const int hashMultiplier = -1521134295;
        var fieldBuilders = fields as FieldBuilder[] ?? fields.ToArray();
        foreach (var field in fieldBuilders)
        {
            unchecked
            {
                hashSeed = hashSeed * hashMultiplier + field.Name.GetHashCode();
            }
        }

        il.Emit(OpCodes.Ldc_I4, hashSeed);
        il.Emit(OpCodes.Stloc_0);

        foreach (var field in fieldBuilders)
        {
            var equalityComparerGenericTypeDefinition = typeof(EqualityComparer<>);
            var equalityComparerDefaultGenericPropertyGetterDefinition = equalityComparerGenericTypeDefinition.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!.GetGetMethod()!;
            var equalityComparerGetHashCodeGenericMethodDefinition = equalityComparerGenericTypeDefinition.GetMethods().Single(m => m.Name == "GetHashCode" && m.GetParameters().Length == 1);

            var equalityComparerType = equalityComparerGenericTypeDefinition.MakeGenericType(field.FieldType);
            var equalityComparerDefaultPropertyGetter = TypeBuilder.GetMethod(equalityComparerType, equalityComparerDefaultGenericPropertyGetterDefinition);
            var equalityComparerGetHashCodeMethod = TypeBuilder.GetMethod(equalityComparerType, equalityComparerGetHashCodeGenericMethodDefinition);

            il.Emit(OpCodes.Ldc_I4, hashMultiplier);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Mul);

            il.EmitCall(OpCodes.Call, equalityComparerDefaultPropertyGetter, null);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, field);
            il.EmitCall(OpCodes.Callvirt, equalityComparerGetHashCodeMethod, null);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc_0);
        }

        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Stloc_1);
        il.Emit(OpCodes.Ldloc_1);
        il.Emit(OpCodes.Ret);

        typeBuilder.DefineMethodOverride(getHashCodeMethodBuilder, typeof(object).GetMethod("GetHashCode")!);
    }

    private static void DefineToStringMethod(TypeBuilder typeBuilder, IEnumerable<KeyValuePair<string, FieldBuilder>> fieldPairs)
    {
        var toStringMethodBuilder = typeBuilder.DefineMethod("ToString", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final, typeof(string), Type.EmptyTypes);

        var il = toStringMethodBuilder.GetILGenerator();

        il.DeclareLocal(typeof(StringBuilder));
        il.DeclareLocal(typeof(string));

        il.Emit(OpCodes.Newobj, typeof(StringBuilder).GetConstructor(Type.EmptyTypes)!);
        il.Emit(OpCodes.Stloc_0);

        var appendStringMethod = typeof(StringBuilder).GetMethod("Append", [typeof(string)])!;
        var appendObjectMethod = typeof(StringBuilder).GetMethod("Append", [typeof(object)])!;

        var isFirst = true;
        foreach (var pair in fieldPairs)
        {
            var propertyName = pair.Key;
            var field = pair.Value;

            var sb = new StringBuilder();
            if (isFirst)
            {
                sb.Append("{ ");
                isFirst = false;
            }
            else
            {
                sb.Append(", ");
            }

            sb.Append(propertyName);
            sb.Append(" = ");

            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ldstr, sb.ToString());
            il.Emit(OpCodes.Callvirt, appendStringMethod);
            il.Emit(OpCodes.Pop);

            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Box, field.FieldType);
            il.Emit(OpCodes.Callvirt, appendObjectMethod);
            il.Emit(OpCodes.Pop);
        }

        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ldstr, " }");
        il.Emit(OpCodes.Callvirt, appendStringMethod);
        il.Emit(OpCodes.Pop);

        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Callvirt, typeof(object).GetMethod("ToString")!);
        il.Emit(OpCodes.Stloc_1);
        il.Emit(OpCodes.Ldloc_1);
        il.Emit(OpCodes.Ret);

        typeBuilder.DefineMethodOverride(toStringMethodBuilder, typeof(object).GetMethod("ToString")!);
    }
}

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Core;

internal static class DeepClonerExprGenerator
{
    private static readonly ConcurrentDictionary<FieldInfo, bool> ReadonlyFields = new();
    private static readonly MethodInfo FieldSetMethod =
        DeepCloneHelpers.RequireMethod(typeof(FieldInfo), "SetValue", [typeof(object), typeof(object)]);

    internal static object GenerateClonerInternal(Type realType, bool asObject)
        => GenerateProcessMethod(realType, asObject && realType.IsValueType);

    internal static void ForceSetField(FieldInfo field, object obj, object? value)
    {
        var attributesField = field.GetType().GetDeclaredField("m_fieldAttributes");
        if (attributesField is null)
        {
            return;
        }

        var attributesValue = attributesField.GetValue(field);
        if (attributesValue is not FieldAttributes attributes)
        {
            return;
        }

        lock (attributesField)
        {
            attributesField.SetValue(field, attributes & ~FieldAttributes.InitOnly);
            field.SetValue(obj, value);
            attributesField.SetValue(field, attributes | FieldAttributes.InitOnly);
        }
    }

    private static object GenerateProcessMethod(Type type, bool unboxStruct)
    {
        if (type.IsArray)
        {
            return GenerateProcessArrayMethod(type);
        }

        if (type.FullName is not null
            && type.FullName.StartsWith("System.Tuple`", StringComparison.Ordinal))
        {
            var genericArguments = type.GenericTypeArguments;
            if (genericArguments.Length < 10 && genericArguments.All(DeepClonerSafeTypes.CanReturnSameObject))
            {
                return GenerateProcessTupleMethod(type);
            }
        }

        var methodType = unboxStruct || type.IsClass ? typeof(object) : type;
        var expressionList = new List<Expression>();
        var from = Expression.Parameter(methodType);
        var fromLocal = from;
        var toLocal = Expression.Variable(type);
        var state = Expression.Parameter(typeof(DeepCloneState));

        if (!type.IsValueType)
        {
            var memberwiseClone = DeepCloneHelpers.RequireDeclaredMethod(typeof(object), "MemberwiseClone");
            expressionList.Add(Expression.Assign(toLocal, Expression.Convert(Expression.Call(from, memberwiseClone), type)));

            fromLocal = Expression.Variable(type);
            expressionList.Add(Expression.Assign(fromLocal, Expression.Convert(from, type)));
            expressionList.Add(Expression.Call(
                state,
                DeepCloneHelpers.RequireDeclaredMethod(typeof(DeepCloneState), "AddKnownRef"),
                from,
                toLocal));
        }
        else if (unboxStruct)
        {
            expressionList.Add(Expression.Assign(toLocal, Expression.Unbox(from, type)));
            fromLocal = Expression.Variable(type);
            expressionList.Add(Expression.Assign(fromLocal, toLocal));
        }
        else
        {
            expressionList.Add(Expression.Assign(toLocal, from));
        }

        var fields = new List<FieldInfo>();
        for (var tp = type; tp is not null && tp.Name != "ContextBoundObject"; tp = tp.BaseType)
        {
            fields.AddRange(tp.DeclaredFields(f => !f.IsStatic));
        }

        foreach (var fieldInfo in fields)
        {
            if (DeepClonerSafeTypes.CanReturnSameObject(fieldInfo.FieldType))
            {
                continue;
            }

            var cloneMethod = fieldInfo.FieldType.IsValueType
                ? DeepCloneHelpers.RequireDeclaredMethod(typeof(DeepClonerGenerator), "CloneStructInternal")
                    .MakeGenericMethod(fieldInfo.FieldType)
                : DeepCloneHelpers.RequireDeclaredMethod(typeof(DeepClonerGenerator), "CloneClassInternal");

            Expression call = Expression.Call(cloneMethod, Expression.Field(fromLocal, fieldInfo), state);
            if (!fieldInfo.FieldType.IsValueType)
            {
                call = Expression.Convert(call, fieldInfo.FieldType);
            }

            var isReadonly = ReadonlyFields.GetOrAdd(fieldInfo, static f => f.IsInitOnly);
            if (isReadonly)
            {
                expressionList.Add(Expression.Call(
                    Expression.Constant(fieldInfo),
                    FieldSetMethod,
                    Expression.Convert(toLocal, typeof(object)),
                    Expression.Convert(call, typeof(object))));
            }
            else
            {
                expressionList.Add(Expression.Assign(Expression.Field(toLocal, fieldInfo), call));
            }
        }

        expressionList.Add(Expression.Convert(toLocal, methodType));

        var funcType = typeof(Func<,,>).MakeGenericType(methodType, typeof(DeepCloneState), methodType);
        var blockParams = new List<ParameterExpression>();
        if (from != fromLocal)
        {
            blockParams.Add(fromLocal);
        }

        blockParams.Add(toLocal);
        return Expression.Lambda(funcType, Expression.Block(blockParams, expressionList), from, state).Compile();
    }

    private static object GenerateProcessArrayMethod(Type type)
    {
        var elementType = type.GetElementType()!;
        var rank = type.GetArrayRank();
        MethodInfo methodInfo;

        if (rank != 1 || type != elementType.MakeArrayType())
        {
            methodInfo = rank == 2 && type == elementType.MakeArrayType(2)
                ? DeepCloneHelpers.RequireDeclaredMethod(typeof(DeepClonerGenerator), "Clone2DimArrayInternal")
                    .MakeGenericMethod(elementType)
                : DeepCloneHelpers.RequireDeclaredMethod(typeof(DeepClonerGenerator), "CloneAbstractArrayInternal");
        }
        else
        {
            var methodName = "Clone1DimArrayClassInternal";
            if (DeepClonerSafeTypes.CanReturnSameObject(elementType))
            {
                methodName = "Clone1DimArraySafeInternal";
            }
            else if (elementType.IsValueType)
            {
                methodName = "Clone1DimArrayStructInternal";
            }

            methodInfo = DeepCloneHelpers.RequireDeclaredMethod(typeof(DeepClonerGenerator), methodName)
                .MakeGenericMethod(elementType);
        }

        var from = Expression.Parameter(typeof(object));
        var state = Expression.Parameter(typeof(DeepCloneState));
        var call = Expression.Call(methodInfo, Expression.Convert(from, type), state);
        var funcType = typeof(Func<,,>).MakeGenericType(typeof(object), typeof(DeepCloneState), typeof(object));
        return Expression.Lambda(funcType, call, from, state).Compile();
    }

    private static object GenerateProcessTupleMethod(Type type)
    {
        var from = Expression.Parameter(typeof(object));
        var state = Expression.Parameter(typeof(DeepCloneState));
        var local = Expression.Variable(type);
        var assign = Expression.Assign(local, Expression.Convert(from, type));
        var tupleLength = type.GenericTypeArguments.Length;

        var constructor = Expression.Assign(
            local,
            Expression.New(
                type.DeclaredConstructors().First(x => x.GetParameters().Length == tupleLength),
                type.DeclaredProperties()
                    .OrderBy(x => x.Name)
                    .Where(x => x.CanRead && x.Name.StartsWith("Item", StringComparison.Ordinal) && char.IsDigit(x.Name[4]))
                    .Select(x => Expression.Property(local, x.Name))));

        var funcType = typeof(Func<object, DeepCloneState, object>);
        return Expression.Lambda(
            funcType,
            Expression.Block(
                [local],
                assign,
                constructor,
                Expression.Call(state, DeepCloneHelpers.RequireDeclaredMethod(typeof(DeepCloneState), "AddKnownRef"), from, local),
                from),
            from,
            state).Compile();
    }
}

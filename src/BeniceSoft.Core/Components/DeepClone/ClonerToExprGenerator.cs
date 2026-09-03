using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Core;

internal static class ClonerToExprGenerator
{
    internal static object GenerateClonerInternal(Type realType, bool isDeepClone)
    {
        if (realType.IsValueType)
        {
            throw new InvalidOperationException("Operation is valid only for reference types");
        }

        return GenerateProcessMethod(realType, isDeepClone);
    }

    private static object GenerateProcessMethod(Type type, bool isDeepClone)
    {
        if (type.IsArray)
        {
            return GenerateProcessArrayMethod(type, isDeepClone);
        }

        var methodType = typeof(object);
        var expressionList = new List<Expression>();
        var from = Expression.Parameter(methodType);
        var to = Expression.Parameter(methodType);
        var state = Expression.Parameter(typeof(DeepCloneState));
        var fromLocal = Expression.Variable(type);
        var toLocal = Expression.Variable(type);

        expressionList.Add(Expression.Assign(fromLocal, Expression.Convert(from, type)));
        expressionList.Add(Expression.Assign(toLocal, Expression.Convert(to, type)));

        if (isDeepClone)
        {
            expressionList.Add(Expression.Call(
                state,
                DeepCloneHelpers.RequireDeclaredMethod(typeof(DeepCloneState), "AddKnownRef"),
                from,
                to));
        }

        var fields = new List<FieldInfo>();
        for (var tp = type; tp is not null && tp.Name != "ContextBoundObject"; tp = tp.BaseType)
        {
            fields.AddRange(tp.DeclaredFields(f => !f.IsStatic));
        }

        foreach (var fieldInfo in fields)
        {
            if (isDeepClone && !DeepClonerSafeTypes.CanReturnSameObject(fieldInfo.FieldType))
            {
                var cloneMethod = fieldInfo.FieldType.IsValueType
                    ? DeepCloneHelpers.RequireDeclaredMethod(typeof(DeepClonerGenerator), "CloneStructInternal")
                        .MakeGenericMethod(fieldInfo.FieldType)
                    : DeepCloneHelpers.RequireDeclaredMethod(typeof(DeepClonerGenerator), "CloneClassInternal");

                Expression call = Expression.Call(cloneMethod, Expression.Field(fromLocal, fieldInfo), state);
                if (!fieldInfo.FieldType.IsValueType)
                {
                    call = Expression.Convert(call, fieldInfo.FieldType);
                }

                if (fieldInfo.IsInitOnly)
                {
                    var setMethod = DeepCloneHelpers.RequireDeclaredMethod(typeof(DeepClonerExprGenerator), "ForceSetField");
                    expressionList.Add(Expression.Call(
                        setMethod,
                        Expression.Constant(fieldInfo),
                        Expression.Convert(toLocal, typeof(object)),
                        Expression.Convert(call, typeof(object))));
                }
                else
                {
                    expressionList.Add(Expression.Assign(Expression.Field(toLocal, fieldInfo), call));
                }
            }
            else
            {
                expressionList.Add(Expression.Assign(
                    Expression.Field(toLocal, fieldInfo),
                    Expression.Field(fromLocal, fieldInfo)));
            }
        }

        expressionList.Add(Expression.Convert(toLocal, methodType));

        var funcType = typeof(Func<,,,>).MakeGenericType(methodType, methodType, typeof(DeepCloneState), methodType);
        return Expression.Lambda(
            funcType,
            Expression.Block([fromLocal, toLocal], expressionList),
            from,
            to,
            state).Compile();
    }

    private static object GenerateProcessArrayMethod(Type type, bool isDeep)
    {
        var elementType = type.GetElementType()!;
        var rank = type.GetArrayRank();
        var from = Expression.Parameter(typeof(object));
        var to = Expression.Parameter(typeof(object));
        var state = Expression.Parameter(typeof(DeepCloneState));
        var funcType = typeof(Func<,,,>).MakeGenericType(typeof(object), typeof(object), typeof(DeepCloneState), typeof(object));

        if (rank == 1 && type == elementType.MakeArrayType())
        {
            if (!isDeep)
            {
                var call = Expression.Call(
                    DeepCloneHelpers.RequireDeclaredMethod(typeof(ClonerToExprGenerator), "ShallowClone1DimArraySafeInternal")
                        .MakeGenericMethod(elementType),
                    Expression.Convert(from, type),
                    Expression.Convert(to, type));
                return Expression.Lambda(funcType, call, from, to, state).Compile();
            }

            var methodName = "Clone1DimArrayClassInternal";
            if (DeepClonerSafeTypes.CanReturnSameObject(elementType))
            {
                methodName = "Clone1DimArraySafeInternal";
            }
            else if (elementType.IsValueType)
            {
                methodName = "Clone1DimArrayStructInternal";
            }

            var methodInfo = DeepCloneHelpers.RequireDeclaredMethod(typeof(ClonerToExprGenerator), methodName)
                .MakeGenericMethod(elementType);
            var deepCall = Expression.Call(
                methodInfo,
                Expression.Convert(from, type),
                Expression.Convert(to, type),
                state);
            return Expression.Lambda(funcType, deepCall, from, to, state).Compile();
        }

        var multiMethod = rank == 2 && type == elementType.MakeArrayType(2)
            ? DeepCloneHelpers.RequireDeclaredMethod(typeof(ClonerToExprGenerator), "Clone2DimArrayInternal")
                .MakeGenericMethod(elementType)
            : DeepCloneHelpers.RequireDeclaredMethod(typeof(ClonerToExprGenerator), "CloneAbstractArrayInternal");

        var multiCall = Expression.Call(
            multiMethod,
            Expression.Convert(from, type),
            Expression.Convert(to, type),
            state,
            Expression.Constant(isDeep));
        return Expression.Lambda(funcType, multiCall, from, to, state).Compile();
    }

    internal static T[] ShallowClone1DimArraySafeInternal<T>(T[] objFrom, T[] objTo)
    {
        var length = Math.Min(objFrom.Length, objTo.Length);
        Array.Copy(objFrom, objTo, length);
        return objTo;
    }

    internal static T[] Clone1DimArraySafeInternal<T>(T[] objFrom, T[] objTo, DeepCloneState state)
    {
        var length = Math.Min(objFrom.Length, objTo.Length);
        state.AddKnownRef(objFrom, objTo);
        Array.Copy(objFrom, objTo, length);
        return objTo;
    }

    internal static T[]? Clone1DimArrayStructInternal<T>(T[]? objFrom, T[]? objTo, DeepCloneState state)
    {
        if (objFrom is null || objTo is null)
        {
            return null;
        }

        var length = Math.Min(objFrom.Length, objTo.Length);
        state.AddKnownRef(objFrom, objTo);
        var cloner = DeepClonerGenerator.GetClonerForValueType<T>()!;
        for (var i = 0; i < length; i++)
        {
            objTo[i] = cloner(objFrom[i], state);
        }

        return objTo;
    }

    internal static T[]? Clone1DimArrayClassInternal<T>(T[]? objFrom, T[]? objTo, DeepCloneState state)
    {
        if (objFrom is null || objTo is null)
        {
            return null;
        }

        var length = Math.Min(objFrom.Length, objTo.Length);
        state.AddKnownRef(objFrom, objTo);
        for (var i = 0; i < length; i++)
        {
            objTo[i] = (T)DeepClonerGenerator.CloneClassInternal(objFrom[i], state)!;
        }

        return objTo;
    }

    internal static T[,]? Clone2DimArrayInternal<T>(T[,]? objFrom, T[,]? objTo, DeepCloneState state, bool isDeep)
    {
        if (objFrom is null || objTo is null)
        {
            return null;
        }

        if (objFrom.GetLowerBound(0) != 0 || objFrom.GetLowerBound(1) != 0
            || objTo.GetLowerBound(0) != 0 || objTo.GetLowerBound(1) != 0)
        {
            return (T[,])CloneAbstractArrayInternal(objFrom, objTo, state, isDeep)!;
        }

        var l1 = Math.Min(objFrom.GetLength(0), objTo.GetLength(0));
        var l2 = Math.Min(objFrom.GetLength(1), objTo.GetLength(1));
        state.AddKnownRef(objFrom, objTo);

        if ((!isDeep || DeepClonerSafeTypes.CanReturnSameObject(typeof(T)))
            && objFrom.GetLength(0) == objTo.GetLength(0)
            && objFrom.GetLength(1) == objTo.GetLength(1))
        {
            Array.Copy(objFrom, objTo, objFrom.Length);
            return objTo;
        }

        if (!isDeep)
        {
            for (var i = 0; i < l1; i++)
            {
                for (var k = 0; k < l2; k++)
                {
                    objTo[i, k] = objFrom[i, k];
                }
            }

            return objTo;
        }

        if (typeof(T).IsValueType)
        {
            var cloner = DeepClonerGenerator.GetClonerForValueType<T>()!;
            for (var i = 0; i < l1; i++)
            {
                for (var k = 0; k < l2; k++)
                {
                    objTo[i, k] = cloner(objFrom[i, k], state);
                }
            }
        }
        else
        {
            for (var i = 0; i < l1; i++)
            {
                for (var k = 0; k < l2; k++)
                {
                    objTo[i, k] = (T)DeepClonerGenerator.CloneClassInternal(objFrom[i, k], state)!;
                }
            }
        }

        return objTo;
    }

    internal static Array? CloneAbstractArrayInternal(Array? objFrom, Array? objTo, DeepCloneState state, bool isDeep)
    {
        if (objFrom is null || objTo is null)
        {
            return null;
        }

        var rank = objFrom.Rank;
        if (objTo.Rank != rank)
        {
            throw new InvalidOperationException("Invalid rank of target array");
        }

        var lowerBoundsFrom = Enumerable.Range(0, rank).Select(objFrom.GetLowerBound).ToArray();
        var lowerBoundsTo = Enumerable.Range(0, rank).Select(objTo.GetLowerBound).ToArray();
        var lengths = Enumerable.Range(0, rank).Select(x => Math.Min(objFrom.GetLength(x), objTo.GetLength(x))).ToArray();
        var indexesFrom = Enumerable.Range(0, rank).Select(objFrom.GetLowerBound).ToArray();
        var indexesTo = Enumerable.Range(0, rank).Select(objTo.GetLowerBound).ToArray();
        state.AddKnownRef(objFrom, objTo);

        if (lengths.Exists(x => x == 0))
        {
            return objTo;
        }

        while (true)
        {
            if (isDeep)
            {
                objTo.SetValue(DeepClonerGenerator.CloneClassInternal(objFrom.GetValue(indexesFrom), state), indexesTo);
            }
            else
            {
                objTo.SetValue(objFrom.GetValue(indexesFrom), indexesTo);
            }

            var ofs = rank - 1;
            while (true)
            {
                indexesFrom[ofs]++;
                indexesTo[ofs]++;
                if (indexesFrom[ofs] >= lowerBoundsFrom[ofs] + lengths[ofs])
                {
                    indexesFrom[ofs] = lowerBoundsFrom[ofs];
                    indexesTo[ofs] = lowerBoundsTo[ofs];
                    ofs--;
                    if (ofs < 0)
                    {
                        return objTo;
                    }
                }
                else
                {
                    break;
                }
            }
        }
    }
}

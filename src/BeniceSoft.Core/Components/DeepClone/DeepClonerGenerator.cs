namespace BeniceSoft.Core;

internal static class DeepClonerGenerator
{
    public static T CloneObject<T>(T obj)
    {
        if (obj is ValueType)
        {
            var type = obj.GetType();
            if (typeof(T) == type)
            {
                if (DeepClonerSafeTypes.CanReturnSameObject(type))
                {
                    return obj;
                }

                return CloneStructInternal(obj, new DeepCloneState());
            }
        }

        return (T)CloneClassRoot(obj)!;
    }

    private static object? CloneClassRoot(object? obj)
    {
        if (obj is null)
        {
            return null;
        }

        var cloner = (Func<object, DeepCloneState, object>?)DeepClonerCache.GetOrAddClass(
            obj.GetType(),
            t => (Func<object, DeepCloneState, object>?)GenerateCloner(t, true));

        return cloner is null ? obj : cloner(obj, new DeepCloneState());
    }

    internal static object? CloneClassInternal(object? obj, DeepCloneState state)
    {
        if (obj is null)
        {
            return null;
        }

        var cloner = (Func<object, DeepCloneState, object>?)DeepClonerCache.GetOrAddClass(
            obj.GetType(),
            t => (Func<object, DeepCloneState, object>?)GenerateCloner(t, true));

        if (cloner is null)
        {
            return obj;
        }

        var knownRef = state.GetKnownRef(obj);
        return knownRef ?? cloner(obj, state);
    }

    private static T CloneStructInternal<T>(T obj, DeepCloneState state)
    {
        var cloner = GetClonerForValueType<T>();
        return cloner is null ? obj : cloner(obj, state);
    }

    internal static T[] Clone1DimArraySafeInternal<T>(T[] obj, DeepCloneState state)
    {
        var outArray = new T[obj.Length];
        state.AddKnownRef(obj, outArray);
        Array.Copy(obj, outArray, obj.Length);
        return outArray;
    }

    internal static T[]? Clone1DimArrayStructInternal<T>(T[]? obj, DeepCloneState state)
    {
        if (obj is null)
        {
            return null;
        }

        var length = obj.Length;
        var outArray = new T[length];
        state.AddKnownRef(obj, outArray);
        var cloner = GetClonerForValueType<T>()!;
        for (var i = 0; i < length; i++)
        {
            outArray[i] = cloner(obj[i], state);
        }

        return outArray;
    }

    internal static T[]? Clone1DimArrayClassInternal<T>(T[]? obj, DeepCloneState state)
    {
        if (obj is null)
        {
            return null;
        }

        var length = obj.Length;
        var outArray = new T[length];
        state.AddKnownRef(obj, outArray);
        for (var i = 0; i < length; i++)
        {
            outArray[i] = (T)CloneClassInternal(obj[i], state)!;
        }

        return outArray;
    }

    internal static T[,]? Clone2DimArrayInternal<T>(T[,]? obj, DeepCloneState state)
    {
        if (obj is null)
        {
            return null;
        }

        var lb1 = obj.GetLowerBound(0);
        var lb2 = obj.GetLowerBound(1);
        if (lb1 != 0 || lb2 != 0)
        {
            return (T[,])CloneAbstractArrayInternal(obj, state)!;
        }

        var l1 = obj.GetLength(0);
        var l2 = obj.GetLength(1);
        var outArray = new T[l1, l2];
        state.AddKnownRef(obj, outArray);

        if (DeepClonerSafeTypes.CanReturnSameObject(typeof(T)))
        {
            Array.Copy(obj, outArray, obj.Length);
            return outArray;
        }

        if (typeof(T).IsValueType)
        {
            var cloner = GetClonerForValueType<T>()!;
            for (var i = 0; i < l1; i++)
            {
                for (var k = 0; k < l2; k++)
                {
                    outArray[i, k] = cloner(obj[i, k], state);
                }
            }
        }
        else
        {
            for (var i = 0; i < l1; i++)
            {
                for (var k = 0; k < l2; k++)
                {
                    outArray[i, k] = (T)CloneClassInternal(obj[i, k], state)!;
                }
            }
        }

        return outArray;
    }

    internal static Array? CloneAbstractArrayInternal(Array? obj, DeepCloneState state)
    {
        if (obj is null)
        {
            return null;
        }

        var rank = obj.Rank;
        var lengths = Enumerable.Range(0, rank).Select(obj.GetLength).ToArray();
        var lowerBounds = Enumerable.Range(0, rank).Select(obj.GetLowerBound).ToArray();
        var indexes = Enumerable.Range(0, rank).Select(obj.GetLowerBound).ToArray();
        var elementType = obj.GetType().GetElementType()!;
        var outArray = Array.CreateInstance(elementType, lengths, lowerBounds);
        state.AddKnownRef(obj, outArray);

        if (lengths.Exists(x => x == 0))
        {
            return outArray;
        }

        if (DeepClonerSafeTypes.CanReturnSameObject(elementType))
        {
            Array.Copy(obj, outArray, obj.Length);
            return outArray;
        }

        var ofs = rank - 1;
        while (true)
        {
            outArray.SetValue(CloneClassInternal(obj.GetValue(indexes), state), indexes);
            indexes[ofs]++;

            if (indexes[ofs] >= lowerBounds[ofs] + lengths[ofs])
            {
                do
                {
                    if (ofs == 0)
                    {
                        return outArray;
                    }

                    indexes[ofs] = lowerBounds[ofs];
                    ofs--;
                    indexes[ofs]++;
                }
                while (indexes[ofs] >= lowerBounds[ofs] + lengths[ofs]);

                ofs = rank - 1;
            }
        }
    }

    internal static Func<T, DeepCloneState, T>? GetClonerForValueType<T>()
        => (Func<T, DeepCloneState, T>?)DeepClonerCache.GetOrAddStructAsObject(
            typeof(T),
            t => (Func<T, DeepCloneState, T>?)GenerateCloner(t, false));

    private static object? GenerateCloner(Type type, bool asObject)
    {
        if (DeepClonerSafeTypes.CanReturnSameObject(type) && asObject && !type.IsValueType)
        {
            return null;
        }

        return DeepClonerExprGenerator.GenerateClonerInternal(type, asObject);
    }

    public static object? CloneObjectTo(object? objFrom, object? objTo, bool isDeep)
    {
        if (objTo is null)
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(objFrom);

        var type = objFrom.GetType();
        if (!type.IsInstanceOfType(objTo))
        {
            throw new InvalidOperationException(
                $"From object type {objFrom.GetType().FullName} is not compatible with target {objTo.GetType().FullName}");
        }

        if (objFrom is string)
        {
            throw new InvalidOperationException("It is forbidden to clone strings");
        }

        var cloner = (Func<object, object, DeepCloneState, object>?)(isDeep
            ? DeepClonerCache.GetOrAddDeepClassTo(type, t => (Func<object, object, DeepCloneState, object>?)ClonerToExprGenerator.GenerateClonerInternal(t, true))
            : DeepClonerCache.GetOrAddShallowClassTo(type, t => (Func<object, object, DeepCloneState, object>?)ClonerToExprGenerator.GenerateClonerInternal(t, false)));

        return cloner is null ? objTo : cloner(objFrom, objTo, new DeepCloneState());
    }
}

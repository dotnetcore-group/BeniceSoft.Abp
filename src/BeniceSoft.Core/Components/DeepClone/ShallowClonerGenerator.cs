namespace BeniceSoft.Core;

internal static class ShallowClonerGenerator
{
    public static T CloneObject<T>(T obj)
    {
        if (obj is ValueType)
        {
            if (typeof(T) == obj.GetType())
            {
                return obj;
            }

            return (T)ShallowObjectCloner.CloneObject(obj);
        }

        if (obj is null)
        {
            return default!;
        }

        if (DeepClonerSafeTypes.CanReturnSameObject(obj.GetType()))
        {
            return obj;
        }

        return (T)ShallowObjectCloner.CloneObject(obj);
    }
}

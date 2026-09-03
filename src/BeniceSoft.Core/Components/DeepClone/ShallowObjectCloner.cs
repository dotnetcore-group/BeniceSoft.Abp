using System.Linq.Expressions;

namespace BeniceSoft.Core;

/// <summary>浅拷贝底层实现（MemberwiseClone）。</summary>
public abstract class ShallowObjectCloner
{
    private static readonly ShallowObjectCloner UnsafeInstance;
    private static ShallowObjectCloner Instance;

    protected abstract object DoCloneObject(object obj);

    public static object CloneObject(object obj)
        => Instance.DoCloneObject(obj);

    internal static bool IsSafeVariant()
        => Instance is ShallowSafeObjectCloner;

    static ShallowObjectCloner()
    {
        Instance = new ShallowSafeObjectCloner();
        UnsafeInstance = Instance;
    }

    internal static void SwitchTo(bool isSafe)
    {
        DeepClonerCache.ClearCache();
        Instance = isSafe ? new ShallowSafeObjectCloner() : UnsafeInstance;
    }

    private sealed class ShallowSafeObjectCloner : ShallowObjectCloner
    {
        private static readonly Func<object, object> CloneFunc;

        static ShallowSafeObjectCloner()
        {
            var methodInfo = DeepCloneHelpers.RequireDeclaredMethod(typeof(object), "MemberwiseClone");
            var parameter = Expression.Parameter(typeof(object));
            var call = Expression.Call(parameter, methodInfo);
            CloneFunc = Expression.Lambda<Func<object, object>>(call, parameter).Compile();
        }

        protected override object DoCloneObject(object obj)
            => CloneFunc(obj);
    }
}

using System.Security;

namespace BeniceSoft.Core;

/// <summary>
/// 深拷贝
/// <para>
/// <b>DeepClone</b>：完整复制对象图（含引用字段、循环引用）。<br/>
/// <b>ShallowClone</b>：仅 MemberwiseClone，不递归克隆依赖。
/// </para>
/// </summary>
public static class DeepCloner
{
    static DeepCloner()
    {
        if (!PermissionCheck())
        {
            throw new SecurityException(
                "DeepCloner should have enough permissions to run. Grant FullTrust or Reflection permission.");
        }
    }

    private static bool PermissionCheck()
    {
        try
        {
            ShallowClone(new object());
            return true;
        }
        catch (VerificationException)
        {
            return false;
        }
        catch (MemberAccessException)
        {
            return false;
        }
    }

    /// <summary>深拷贝：复制对象及其引用图（循环引用安全）。</summary>
    public static T DeepClone<T>(T obj)
        => DeepClonerGenerator.CloneObject(obj);

    /// <summary>深拷贝到已有目标实例（类类型；实际运行时类型应兼容）。</summary>
    public static TTo DeepClone<TFrom, TTo>(TFrom objFrom, TTo objTo)
        where TTo : class, TFrom
        => (TTo)DeepClonerGenerator.CloneObjectTo(objFrom, objTo, true)!;

    /// <summary>浅拷贝到已有目标实例（不递归克隆依赖）。</summary>
    public static TTo ShallowClone<TFrom, TTo>(TFrom objFrom, TTo objTo)
        where TTo : class, TFrom
        => (TTo)DeepClonerGenerator.CloneObjectTo(objFrom, objTo, false)!;

    /// <summary>浅拷贝：仅新建对象，依赖引用保持共享。</summary>
    public static T ShallowClone<T>(T obj)
        => ShallowClonerGenerator.CloneObject(obj);
}

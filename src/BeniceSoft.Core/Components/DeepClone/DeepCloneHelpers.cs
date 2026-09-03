using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace BeniceSoft.Core;

internal static class DeepCloneHelpers
{
    [return: NotNull]
    public static MethodInfo RequireDeclaredMethod(Type type, string name)
        => type.GetDeclaredMethod(name)
           ?? throw new MissingMethodException(type.FullName, name);

    [return: NotNull]
    public static MethodInfo RequireMethod(Type type, string name, Type[] parameterTypes)
        => type.GetMethod(name, parameterTypes)
           ?? throw new MissingMethodException(type.FullName, name);
}

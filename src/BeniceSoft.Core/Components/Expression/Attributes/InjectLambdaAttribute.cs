using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Core;

/// <summary>
/// Marks a method as injectable.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
public sealed class InjectLambdaAttribute : Attribute
{
    /// <summary>
    /// The target type for the method's expression. The current type, if null.
    /// </summary>
    public Type? Target { get; }

    /// <summary>
    /// The method's name for creating the method's expression. The same name, if null or empty.
    /// </summary>
    public string Method { get; } = string.Empty;

    internal static InjectLambdaAttribute None { get; } = new InjectLambdaAttribute();

    /// <summary>
    /// Marks a method as injectable.
    /// </summary>
    public InjectLambdaAttribute()
    {
    }

    /// <summary>
    /// Marks a method as injectable.
    /// </summary>
    /// <param name="target">The target type for the method's expression.</param>
    public InjectLambdaAttribute(Type target)
    {
        ArgumentNullException.ThrowIfNull(target);

        Target = target;
    }

    /// <summary>
    /// Marks a method as injectable.
    /// </summary>
    /// <param name="target">The target type for the method's expression.</param>
    /// <param name="method">The method's name for creating the method's expression.</param>
    public InjectLambdaAttribute(Type target, string method)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNullOrEmpty(method);

        Target = target;
        Method = method;
    }

    /// <summary>
    /// Marks a method as injectable.
    /// </summary>
    /// <param name="method">The method's name for creating the method's expression.</param>
    public InjectLambdaAttribute(string method)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(method);

        Method = method;
    }

    /// <summary>
    /// Gets the current attribute provider. Default is the standard reflection-based provider.
    /// </summary>
    public static Func<MemberInfo, InjectLambdaAttribute?> Provider { get; private set; } = m => m.GetCustomAttribute<InjectLambdaAttribute>();

    /// <summary>
    /// Sets a custom attribute provider.
    /// </summary>
    /// <param name="provider">The custom attribute provider to set.</param>
    /// <exception cref="ArgumentNullException">Thrown if provider is null.</exception>
    public static void SetProvider(Func<MemberInfo, InjectLambdaAttribute?> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        Provider = provider;
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:删除未使用的私有成员", Justification = "<挂起>")]
internal static class InjectLambdaExtensions
{
    private static Expression<Func<string, bool>> IsEmpty()
    {
        return c => string.IsNullOrEmpty(c);
    }

    private static Expression<Func<string, bool>> IsNotEmpty()
    {
        return c => !string.IsNullOrEmpty(c);
    }

    private static Expression<Func<string, bool>> IsNull()
    {
        return c => string.IsNullOrWhiteSpace(c);
    }

    private static Expression<Func<string, bool>> IsNotNull()
    {
        return c => !string.IsNullOrWhiteSpace(c);
    }

    private static Expression<Func<T, T[], bool>> In<T>()
    {
        return (c, p) => p.Contains(c);
    }

    private static Expression<Func<T, T[], bool>> NotIn<T>()
    {
        return (c, p) => !p.Contains(c);
    }
}
namespace BeniceSoft.Core.Reflector;

public enum CallRefOptions
{
    Call,
    Callvirt
}

internal interface IParameterReflectorProvider
{
    ParameterReflector[] ParameterReflectors { get; }
}

public interface ICustomAttributeReflectorProvider
{
    CustomAttributeReflector[] CustomAttributeReflectors { get; }
}

/// <summary>
/// https://github.com/zkweb-framework/ZKWeb/blob/master/ZKWeb/ZKWebStandard/Collections/Pair.cs
/// </summary>
/// <typeparam name="T1"></typeparam>
/// <typeparam name="T2"></typeparam>
internal struct Pair<T1, T2>(T1 first, T2 second) : IEquatable<Pair<T1, T2>>
{
    public T1 Item1 { get; private set; } = first;

    public T2 Item2 { get; private set; } = second;

    public readonly bool Equals(Pair<T1, T2> obj)
    {
        return Item1!.Equals(obj.Item1) && Item2!.Equals(obj.Item2);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Pair<T1, T2> pair && Equals(pair);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(Item1, Item2);
    }

    public override readonly string ToString()
    {
        return $"({Item1?.ToString() ?? "null"}, {Item2?.ToString() ?? "null"})";
    }

    public readonly void Deconstruct(out T1 first, out T2 second)
    {
        first = Item1;
        second = Item2;
    }
}

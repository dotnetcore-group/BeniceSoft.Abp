using System.Collections.Concurrent;
using System.Reflection;

namespace BeniceSoft.Core.Reflector;

internal struct MethodSignature
{
    private static readonly ConcurrentDictionary<Pair<MethodBase, string>, int> _signatures = new();

    public int Value { get; }

    public string Name { get; set; }

    public MethodSignature(MethodBase method) : this(method, method?.Name!)
    {
    }

    public MethodSignature(MethodBase method, string name)
    {
        ArgumentNullException.ThrowIfNull(method);

        Name = name;
        Value = _signatures.GetOrAdd(new Pair<MethodBase, string>(method, name), GetSignatureCode);
    }

    public override readonly bool Equals(object? obj)
    {
        if (obj is MethodSignature signature)
        {
            return Value == signature.Value;
        }

        return false;
    }

    public override readonly int GetHashCode()
    {
        return Value;
    }

    public static bool operator !=(MethodSignature signature, MethodSignature other)
    {
        return signature.Value != other.Value;
    }

    public static bool operator ==(MethodSignature signature, MethodSignature other)
    {
        return signature.Value == other.Value;
    }

    private static int GetSignatureCode(Pair<MethodBase, string> pair)
    {
        var method = pair.Item1;
        var name = pair.Item2 ?? method.Name;
        var parameterTypes = method.GetParameterTypes();
        var signatureCode = HashCode.Combine(name, parameterTypes.Length);
        if (parameterTypes.Length > 0)
        {
            foreach (var paramterType in parameterTypes)
            {
                if (paramterType.IsGenericParameter)
                {
                    continue;
                }
                else if (paramterType.IsGenericType())
                {
                    signatureCode = GetSignatureCode(signatureCode, paramterType);
                }
                else
                {
                    signatureCode = HashCode.Combine(signatureCode, paramterType);
                }
            }
        }

        if (method.IsGenericMethod)
        {
            signatureCode = HashCode.Combine(signatureCode, method.GetGenericArguments().Length);
        }

        return signatureCode;
    }

    private static int GetSignatureCode(int signatureCode, Type genericType)
    {
        signatureCode = HashCode.Combine(signatureCode, genericType.GetGenericTypeDefinition(), genericType.GenericTypeArguments.Length);
        foreach (var argument in genericType.GenericTypeArguments)
        {
            if (argument.IsGenericParameter)
            {
                continue;
            }
            else if (argument.IsGenericType())
            {
                signatureCode = GetSignatureCode(signatureCode, argument);
            }
            else
            {
                signatureCode = HashCode.Combine(signatureCode, argument);
            }
        }

        return signatureCode;
    }
}

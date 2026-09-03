using System.Reflection;

namespace BeniceSoft.Core.Reflector;

public class ParameterReflector : ICustomAttributeReflectorProvider
{
    private readonly ParameterInfo _reflectionInfo;

    public CustomAttributeReflector[] CustomAttributeReflectors { get; } = [];

    public string? Name => _reflectionInfo.Name;

    public bool HasDeflautValue { get; }

    public object? DefalutValue { get; }

    public int Position { get; }

    public Type ParameterType { get; }

    private ParameterReflector(ParameterInfo reflectionInfo)
    {
        ArgumentNullException.ThrowIfNull(reflectionInfo);

        _reflectionInfo = reflectionInfo;

        if (!reflectionInfo.ParameterType.IsTupleType())
        {
            CustomAttributeReflectors = _reflectionInfo.CustomAttributes.Select(CustomAttributeReflector.Create).ToArray();
        }

        HasDeflautValue = reflectionInfo.HasDefault();
        if (HasDeflautValue)
        {
            DefalutValue = reflectionInfo.DefaultSafely();
        }

        Position = reflectionInfo.Position;
        ParameterType = reflectionInfo.ParameterType;
    }

    internal static ParameterReflector Create(ParameterInfo parameterInfo)
    {
        ArgumentNullException.ThrowIfNull(parameterInfo);

        return ReflectorCacheUtils<ParameterInfo, ParameterReflector>.GetOrAdd(parameterInfo, info => new ParameterReflector(info));
    }

    public ParameterInfo GetParameterInfo()
    {
        return _reflectionInfo;
    }

    public override string ToString()
    {
        return $"Parameter : {_reflectionInfo}  ParameterType : {ParameterType}";
    }
}

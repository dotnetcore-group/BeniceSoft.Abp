using System.Reflection;

namespace BeniceSoft.Core.Reflector;

public abstract class MemberReflector<TMemberInfo> : ICustomAttributeReflectorProvider
    where TMemberInfo : MemberInfo
{
    protected TMemberInfo ReflectionInfo { get; }

    public virtual string Name => ReflectionInfo.Name;

    public CustomAttributeReflector[] CustomAttributeReflectors { get; }

    protected MemberReflector(TMemberInfo reflectionInfo)
    {
        ArgumentNullException.ThrowIfNull(reflectionInfo);

        ReflectionInfo = reflectionInfo;
        CustomAttributeReflectors = ReflectionInfo.CustomAttributes.Select(CustomAttributeReflector.Create).ToArray();
    }

    public override string ToString()
    {
        return $"{ReflectionInfo.MemberType} : {ReflectionInfo}  DeclaringType : {ReflectionInfo.DeclaringType}";
    }

    public TMemberInfo GetMemberInfo()
    {
        return ReflectionInfo;
    }

    public virtual string DisplayName => ReflectionInfo.Name;
}

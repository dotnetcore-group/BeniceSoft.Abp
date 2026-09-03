using System.Reflection;
using System.Reflection.Emit;

namespace BeniceSoft.Core.Reflector;

public class CustomAttributeReflector
{
    private readonly CustomAttributeData _customAttributeData;
    private readonly Func<Attribute> _invoker;

    internal HashSet<RuntimeTypeHandle> Tokens { get; }

    public Type AttributeType { get; }

    private CustomAttributeReflector(CustomAttributeData customAttributeData)
    {
        ArgumentNullException.ThrowIfNull(customAttributeData);

        _customAttributeData = customAttributeData;
        AttributeType = _customAttributeData.AttributeType;
        _invoker = CreateInvoker();
        Tokens = GetAttrTokens(AttributeType);
    }

    internal static CustomAttributeReflector Create(CustomAttributeData customAttributeData)
    {
        return ReflectorCacheUtils<CustomAttributeData, CustomAttributeReflector>.GetOrAdd(customAttributeData, data => new CustomAttributeReflector(data));
    }

    private Func<Attribute> CreateInvoker()
    {
        var dynamicMethod = new DynamicMethod($"invoker-{Guid.NewGuid()}", typeof(Attribute), null, AttributeType.GetTypeInfo().Module, true);
        var ilGen = dynamicMethod.GetILGenerator();

        foreach (var constructorParameter in _customAttributeData.ConstructorArguments)
        {
            if (constructorParameter.ArgumentType.IsArray())
            {
                ilGen.EmitArray(((IEnumerable<CustomAttributeTypedArgument>)constructorParameter.Value!).Select(x => x.Value).ToArray()!, constructorParameter.ArgumentType.GetTypeInfo().UnWrapArrayType());
            }
            else
            {
                ilGen.EmitConstant(constructorParameter.Value!, constructorParameter.ArgumentType);
            }
        }

        var attributeLocal = ilGen.DeclareLocal(AttributeType);

        ilGen.EmitNew(_customAttributeData.Constructor);

        ilGen.Emit(OpCodes.Stloc, attributeLocal);

        var attributeTypeInfo = AttributeType.GetTypeInfo();

        foreach (var namedArgument in _customAttributeData.NamedArguments)
        {
            ilGen.Emit(OpCodes.Ldloc, attributeLocal);
            if (namedArgument.TypedValue.ArgumentType.IsArray())
            {
                ilGen.EmitArray(((IEnumerable<CustomAttributeTypedArgument>)namedArgument.TypedValue.Value!).
                    Select(x => x.Value).ToArray()!,
                    namedArgument.TypedValue.ArgumentType.GetTypeInfo().UnWrapArrayType());
            }
            else
            {
                ilGen.EmitConstant(namedArgument.TypedValue.Value!, namedArgument.TypedValue.ArgumentType);
            }

            if (namedArgument.IsField)
            {
                var field = attributeTypeInfo.GetField(namedArgument.MemberName);
                ilGen.Emit(OpCodes.Stfld, field!);
            }
            else
            {
                var property = attributeTypeInfo.GetProperty(namedArgument.MemberName);
                ilGen.Emit(OpCodes.Callvirt, property!.SetMethod!);
            }
        }

        ilGen.Emit(OpCodes.Ldloc, attributeLocal);
        ilGen.Emit(OpCodes.Ret);
        return (Func<Attribute>)dynamicMethod.CreateDelegate(typeof(Func<Attribute>));
    }

    private static HashSet<RuntimeTypeHandle> GetAttrTokens(Type attributeType)
    {
        var tokenSet = new HashSet<RuntimeTypeHandle>();
        for (Type? attr = attributeType; attr != typeof(object) && attr != null; attr = attr.GetTypeInfo().BaseType)
        {
            tokenSet.Add(attr.TypeHandle);
        }

        return tokenSet;
    }

    public Attribute Invoke()
    {
        return _invoker();
    }

    public CustomAttributeData GetCustomAttributeData()
    {
        return _customAttributeData;
    }
}

using System.Reflection;
using System.Reflection.Emit;

namespace BeniceSoft.Core.Reflector;

public class ConstructorReflector : MemberReflector<ConstructorInfo>, IParameterReflectorProvider
{
    private readonly Func<object?[]?, object?>? _invoker;

    public ParameterReflector[] ParameterReflectors { get; }

    private ConstructorReflector(ConstructorInfo constructorInfo) : base(constructorInfo)
    {
        _invoker = CreateInvoker();
        ParameterReflectors = constructorInfo.GetParameters().Select(ParameterReflector.Create).ToArray();
    }

    internal static ConstructorReflector Create(ConstructorInfo constructorInfo)
    {
        ArgumentNullException.ThrowIfNull(constructorInfo);

        return ReflectorCacheUtils<ConstructorInfo, ConstructorReflector>.GetOrAdd(constructorInfo, info =>
        {
            if (info.DeclaringType!.GetTypeInfo().ContainsGenericParameters)
            {
                return new OpenGenericConstructorReflector(info);
            }

            return new ConstructorReflector(info);
        });
    }

    protected virtual Func<object?[]?, object?>? CreateInvoker()
    {
        var dynamicMethod = new DynamicMethod($"invoker-{Guid.NewGuid()}", typeof(object), [typeof(object[])], ReflectionInfo.Module, true);
        var ilGen = dynamicMethod.GetILGenerator();

        var parameterTypes = ReflectionInfo.GetParameterTypes();
        if (parameterTypes.Length == 0)
        {
            ilGen.Emit(OpCodes.Newobj, ReflectionInfo);
            return CreateDelegate();
        }

        var refParameterCount = parameterTypes.Count(x => x.IsByRef);
        if (refParameterCount == 0)
        {
            foreach (var i in parameterTypes.Length)
            {
                ilGen.EmitLoadArg(0);
                ilGen.EmitInt(i);
                ilGen.Emit(OpCodes.Ldelem_Ref);
                ilGen.EmitConvertFromObject(parameterTypes[i]);
            }

            ilGen.Emit(OpCodes.Newobj, ReflectionInfo);
            return CreateDelegate();
        }

        var indexedLocals = new IndexedLocalBuilder[refParameterCount];
        var index = 0;
        foreach (var i in parameterTypes.Length)
        {
            ilGen.EmitLoadArg(0);
            ilGen.EmitInt(i);
            ilGen.Emit(OpCodes.Ldelem_Ref);
            if (parameterTypes[i].IsByRef)
            {
                var defType = parameterTypes[i].GetElementType()!;
                var indexedLocal = new IndexedLocalBuilder(ilGen.DeclareLocal(defType), i);
                indexedLocals[index++] = indexedLocal;
                ilGen.EmitConvertFromObject(defType);
                ilGen.Emit(OpCodes.Stloc, indexedLocal.LocalBuilder);
                ilGen.Emit(OpCodes.Ldloca, indexedLocal.LocalBuilder);
            }
            else
            {
                ilGen.EmitConvertFromObject(parameterTypes[i]);
            }
        }

        ilGen.Emit(OpCodes.Newobj, ReflectionInfo);
        foreach (var i in indexedLocals.Length)
        {
            ilGen.EmitLoadArg(0);
            ilGen.EmitInt(indexedLocals[i].Index);
            ilGen.Emit(OpCodes.Ldloc, indexedLocals[i].LocalBuilder);
            ilGen.EmitConvertToObject(indexedLocals[i].LocalType);
            ilGen.Emit(OpCodes.Stelem_Ref);
        }

        return CreateDelegate();

        Func<object?[]?, object?> CreateDelegate()
        {
            var declaringType = ReflectionInfo.DeclaringType!;
            if (declaringType.IsValueType())
            {
                ilGen.EmitConvertToObject(declaringType);
            }

            ilGen.Emit(OpCodes.Ret);
            return (Func<object?[]?, object?>)dynamicMethod.CreateDelegate(typeof(Func<object?[]?, object?>));
        }
    }

    public virtual object? Invoke(params object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return _invoker!(args);
    }

    private sealed class OpenGenericConstructorReflector(ConstructorInfo constructorInfo) : ConstructorReflector(constructorInfo)
    {
        protected override Func<object?[]?, object?>? CreateInvoker()
        {
            return null;
        }

        public override object? Invoke(params object?[]? args)
        {
            throw new InvalidOperationException($"Cannot create an instance of {ReflectionInfo.DeclaringType} because Type.ContainsGenericParameters is true.");
        }
    }
}

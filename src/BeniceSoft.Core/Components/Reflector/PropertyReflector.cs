using System.Reflection;
using System.Reflection.Emit;

namespace BeniceSoft.Core.Reflector;

public class PropertyReflector : MemberReflector<PropertyInfo>
{
    protected Func<object?, object?> Getter { get; }

    protected Action<object?, object?> Setter { get; }

    private PropertyReflector(PropertyInfo reflectionInfo) : base(reflectionInfo)
    {
        Getter = reflectionInfo.CanRead ? CreateGetter() : ins => throw new InvalidOperationException($"Property {ReflectionInfo.Name} does not define get accessor.");
        Setter = reflectionInfo.CanWrite ? CreateSetter() : (ins, val) => throw new InvalidOperationException($"Property {ReflectionInfo.Name} does not define get accessor.");
    }

    internal static PropertyReflector Create(PropertyInfo reflectionInfo, CallRefOptions callOption)
    {
        ArgumentNullException.ThrowIfNull(reflectionInfo);

        return ReflectorCacheUtils<Pair<PropertyInfo, CallRefOptions>, PropertyReflector>.GetOrAdd(new Pair<PropertyInfo, CallRefOptions>(reflectionInfo, callOption), CreateInternal);

        static PropertyReflector CreateInternal(Pair<PropertyInfo, CallRefOptions> item)
        {
            var property = item.Item1;
            if (property.DeclaringType!.GetTypeInfo().ContainsGenericParameters)
            {
                return new OpenGenericPropertyReflector(property);
            }

            if (property.CanRead && property.GetMethod!.IsStatic || property.CanWrite && property.SetMethod!.IsStatic)
            {
                return new StaticPropertyReflector(property);
            }

            if (property.DeclaringType!.IsValueType() || item.Item2 == CallRefOptions.Call)
            {
                return new CallPropertyReflector(property);
            }

            return new PropertyReflector(property);
        }
    }

    protected virtual Func<object?, object?> CreateGetter()
    {
        var dynamicMethod = new DynamicMethod($"getter-{Guid.NewGuid()}", typeof(object), [typeof(object)], ReflectionInfo.Module, true);
        var ilGen = dynamicMethod.GetILGenerator();
        ilGen.EmitLoadArg(0);
        ilGen.EmitConvertFromObject(ReflectionInfo.DeclaringType!);
        ilGen.Emit(OpCodes.Callvirt, ReflectionInfo.GetMethod!);
        if (ReflectionInfo.PropertyType.IsValueType())
        {
            ilGen.EmitConvertToObject(ReflectionInfo.PropertyType);
        }

        ilGen.Emit(OpCodes.Ret);
        return (Func<object?, object?>)dynamicMethod.CreateDelegate(typeof(Func<object?, object?>));
    }

    protected virtual Action<object?, object?> CreateSetter()
    {
        var dynamicMethod = new DynamicMethod($"setter-{Guid.NewGuid()}", typeof(void), [typeof(object), typeof(object)], ReflectionInfo.Module, true);
        var ilGen = dynamicMethod.GetILGenerator();
        ilGen.EmitLoadArg(0);
        ilGen.EmitConvertFromObject(ReflectionInfo.DeclaringType!);
        ilGen.EmitLoadArg(1);
        ilGen.EmitConvertFromObject(ReflectionInfo.PropertyType);
        ilGen.Emit(OpCodes.Callvirt, ReflectionInfo.SetMethod!);
        ilGen.Emit(OpCodes.Ret);
        return (Action<object?, object?>)dynamicMethod.CreateDelegate(typeof(Action<object?, object?>));
    }

    public virtual object? GetValue(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return Getter.Invoke(instance);
    }

    public virtual void SetValue(object instance, object? value)
    {
        ArgumentNullException.ThrowIfNull(instance);

        Setter(instance, value);
    }

    public virtual object? GetStaticValue()
    {
        throw new InvalidOperationException($"Property {ReflectionInfo.Name} must be static to call this method. For get instance property value, call 'GetValue'.");
    }

    public virtual void SetStaticValue(object? value)
    {
        throw new InvalidOperationException($"Property {ReflectionInfo.Name} must be static to call this method. For set instance property value, call 'SetValue'.");
    }

    private sealed class CallPropertyReflector(PropertyInfo reflectionInfo) : PropertyReflector(reflectionInfo)
    {
        protected override Func<object?, object?> CreateGetter()
        {
            var dynamicMethod = new DynamicMethod($"getter-{Guid.NewGuid()}", typeof(object), [typeof(object)], ReflectionInfo.Module, true);
            var ilGen = dynamicMethod.GetILGenerator();
            var declaringType = ReflectionInfo.DeclaringType!;
            ilGen.EmitLoadArg(0);
            ilGen.EmitConvertFromObject(declaringType);
            if (declaringType.IsValueType())
            {
                var local = ilGen.DeclareLocal(declaringType);
                ilGen.Emit(OpCodes.Stloc, local);
                ilGen.Emit(OpCodes.Ldloca, local);
            }

            ilGen.Emit(OpCodes.Call, ReflectionInfo.GetMethod!);
            if (ReflectionInfo.PropertyType.IsValueType())
            {
                ilGen.EmitConvertToObject(ReflectionInfo.PropertyType);
            }

            ilGen.Emit(OpCodes.Ret);
            return (Func<object?, object?>)dynamicMethod.CreateDelegate(typeof(Func<object?, object?>));
        }

        protected override Action<object?, object?> CreateSetter()
        {
            var dynamicMethod = new DynamicMethod($"setter-{Guid.NewGuid()}", typeof(void), [typeof(object), typeof(object)], ReflectionInfo.Module, true);
            var ilGen = dynamicMethod.GetILGenerator();
            var declaringType = ReflectionInfo.DeclaringType!;
            ilGen.EmitLoadArg(0);
            ilGen.EmitConvertFromObject(declaringType);
            if (declaringType.IsValueType())
            {
                var local = ilGen.DeclareLocal(declaringType);
                ilGen.Emit(OpCodes.Stloc, local);
                ilGen.Emit(OpCodes.Ldloca, local);
            }

            ilGen.EmitLoadArg(1);
            ilGen.EmitConvertFromObject(ReflectionInfo.PropertyType);
            ilGen.Emit(OpCodes.Call, ReflectionInfo.SetMethod!);
            ilGen.Emit(OpCodes.Ret);
            return (Action<object?, object?>)dynamicMethod.CreateDelegate(typeof(Action<object?, object?>));
        }
    }

    private sealed class OpenGenericPropertyReflector(PropertyInfo reflectionInfo) : PropertyReflector(reflectionInfo)
    {
        public override object? GetValue(object instance)
        {
            throw new InvalidOperationException("Late bound operations cannot be performed on property with types for which Type.ContainsGenericParameters is true");
        }

        public override void SetValue(object instance, object? value)
        {
            throw new InvalidOperationException("Late bound operations cannot be performed on property with types for which Type.ContainsGenericParameters is true");
        }

        public override object? GetStaticValue()
        {
            throw new InvalidOperationException("Late bound operations cannot be performed on property with types for which Type.ContainsGenericParameters is true");
        }

        public override void SetStaticValue(object? value)
        {
            throw new InvalidOperationException("Late bound operations cannot be performed on property with types for which Type.ContainsGenericParameters is true");
        }
    }

    private sealed class StaticPropertyReflector(PropertyInfo reflectionInfo) : PropertyReflector(reflectionInfo)
    {
        protected override Func<object?, object?> CreateGetter()
        {
            var dynamicMethod = new DynamicMethod($"getter-{Guid.NewGuid()}", typeof(object), [typeof(object)], ReflectionInfo.Module, true);
            var ilGen = dynamicMethod.GetILGenerator();
            ilGen.Emit(OpCodes.Call, ReflectionInfo.GetMethod!);
            if (ReflectionInfo.PropertyType.IsValueType())
            {
                ilGen.EmitConvertToObject(ReflectionInfo.PropertyType);
            }

            ilGen.Emit(OpCodes.Ret);
            return (Func<object?, object?>)dynamicMethod.CreateDelegate(typeof(Func<object?, object?>));
        }

        protected override Action<object?, object?> CreateSetter()
        {
            var dynamicMethod = new DynamicMethod($"setter-{Guid.NewGuid()}", typeof(void), [typeof(object), typeof(object)], ReflectionInfo.Module, true);
            var ilGen = dynamicMethod.GetILGenerator();
            ilGen.EmitLoadArg(1);
            ilGen.EmitConvertFromObject(ReflectionInfo.PropertyType);
            ilGen.Emit(OpCodes.Call, ReflectionInfo.SetMethod!);
            ilGen.Emit(OpCodes.Ret);
            return (Action<object?, object?>)dynamicMethod.CreateDelegate(typeof(Action<object?, object?>));
        }

        public override object? GetValue(object instance)
        {
            return Getter(null);
        }

        public override void SetValue(object instance, object? value)
        {
            Setter(null, value);
        }

        public override object? GetStaticValue()
        {
            return Getter(null);
        }

        public override void SetStaticValue(object? value)
        {
            Setter(null, value);
        }
    }
}

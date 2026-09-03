using System.Reflection;
using System.Reflection.Emit;

namespace BeniceSoft.Core.Reflector;

public class FieldReflector : MemberReflector<FieldInfo>
{
    private readonly Func<object, object>? _getter;
    private readonly Action<object, object>? _setter;

    protected FieldReflector(FieldInfo reflectionInfo) : base(reflectionInfo)
    {
        _getter = CreateGetter();
        _setter = CreateSetter();
    }

    internal static FieldReflector Create(FieldInfo reflectionInfo)
    {
        ArgumentNullException.ThrowIfNull(reflectionInfo);

        return ReflectorCacheUtils<FieldInfo, FieldReflector>.GetOrAdd(reflectionInfo, CreateInternal);

        static FieldReflector CreateInternal(FieldInfo field)
        {
            if (field.DeclaringType!.GetTypeInfo().ContainsGenericParameters)
            {
                return new OpenGenericFieldReflector(field);
            }

            if (field.DeclaringType!.IsEnum())
            {
                return new EnumFieldReflector(field);
            }

            if (field.IsStatic)
            {
                return new StaticFieldReflector(field);
            }

            return new FieldReflector(field);
        }
    }

    protected virtual Func<object, object>? CreateGetter()
    {
        var dynamicMethod = new DynamicMethod($"getter-{Guid.NewGuid()}", typeof(object), [typeof(object)], ReflectionInfo.Module, true);
        var ilGen = dynamicMethod.GetILGenerator();
        ilGen.EmitLoadArg(0);
        ilGen.EmitConvertFromObject(ReflectionInfo.DeclaringType!);
        ilGen.Emit(OpCodes.Ldfld, ReflectionInfo);
        ilGen.EmitConvertToObject(ReflectionInfo.FieldType);
        ilGen.Emit(OpCodes.Ret);
        return (Func<object, object>)dynamicMethod.CreateDelegate(typeof(Func<object, object>));
    }

    protected virtual Action<object, object>? CreateSetter()
    {
        var dynamicMethod = new DynamicMethod($"setter-{Guid.NewGuid()}", typeof(void), [typeof(object), typeof(object)], ReflectionInfo.Module, true);
        var ilGen = dynamicMethod.GetILGenerator();
        ilGen.EmitLoadArg(0);
        ilGen.EmitConvertFromObject(ReflectionInfo.DeclaringType!);
        ilGen.EmitLoadArg(1);
        ilGen.EmitConvertFromObject(ReflectionInfo.FieldType);
        ilGen.Emit(OpCodes.Stfld, ReflectionInfo);
        ilGen.Emit(OpCodes.Ret);
        return (Action<object, object>)dynamicMethod.CreateDelegate(typeof(Action<object, object>));
    }

    public virtual object GetValue(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return _getter!(instance);
    }

    public virtual void SetValue(object instance, object? value)
    {
        ArgumentNullException.ThrowIfNull(instance);

        _setter!(instance, value!);
    }

    public virtual object GetStaticValue()
    {
        throw new InvalidOperationException($"Field {ReflectionInfo.Name} must be static to call this method. For get instance field value, call 'GetValue'.");
    }

    public virtual void SetStaticValue(object? value)
    {
        throw new InvalidOperationException($"Field {ReflectionInfo.Name} must be static to call this method. For set instance field value, call 'SetValue'.");
    }

    private sealed class EnumFieldReflector(FieldInfo reflectionInfo) : FieldReflector(reflectionInfo)
    {
        protected override Func<object, object>? CreateGetter()
        {
            var dynamicMethod = new DynamicMethod($"getter-{Guid.NewGuid()}", typeof(object), [typeof(object)], ReflectionInfo.Module, true);
            var ilGen = dynamicMethod.GetILGenerator();
            var value = ReflectionInfo.GetValue(null);
            ilGen.EmitConstant(value!, ReflectionInfo.FieldType);
            ilGen.EmitConvertToObject(ReflectionInfo.FieldType);
            ilGen.Emit(OpCodes.Ret);
            return (Func<object, object>)dynamicMethod.CreateDelegate(typeof(Func<object, object>));
        }

        protected override Action<object, object>? CreateSetter()
        {
            var dynamicMethod = new DynamicMethod($"setter-{Guid.NewGuid()}", typeof(void), [typeof(object), typeof(object)], ReflectionInfo.Module, true);
            var ilGen = dynamicMethod.GetILGenerator();
            ilGen.Emit(OpCodes.Ret);
            return (Action<object, object>)dynamicMethod.CreateDelegate(typeof(Action<object, object>));
        }

        public override object GetValue(object instance)
        {
            return _getter!(null!);
        }

        public override void SetValue(object instance, object? value)
        {
            throw new FieldAccessException("Cannot set a constant field");
        }

        public override object GetStaticValue()
        {
            return _getter!(null!);
        }

        public override void SetStaticValue(object? value)
        {
            throw new FieldAccessException("Cannot set a constant field");
        }
    }

    private sealed class OpenGenericFieldReflector(FieldInfo reflectionInfo) : FieldReflector(reflectionInfo)
    {
        protected override Func<object, object>? CreateGetter()
        {
            return null;
        }

        protected override Action<object, object>? CreateSetter()
        {
            return null;
        }

        public override object GetValue(object instance)
        {
            throw new InvalidOperationException("Late bound operations cannot be performed on fields with types for which Type.ContainsGenericParameters is true");
        }

        public override void SetValue(object instance, object? value)
        {
            throw new InvalidOperationException("Late bound operations cannot be performed on fields with types for which Type.ContainsGenericParameters is true");
        }

        public override object GetStaticValue()
        {
            throw new InvalidOperationException("Late bound operations cannot be performed on fields with types for which Type.ContainsGenericParameters is true");
        }

        public override void SetStaticValue(object? value)
        {
            throw new InvalidOperationException("Late bound operations cannot be performed on fields with types for which Type.ContainsGenericParameters is true");
        }
    }

    private sealed class StaticFieldReflector(FieldInfo reflectionInfo) : FieldReflector(reflectionInfo)
    {
        protected override Func<object, object>? CreateGetter()
        {
            var dynamicMethod = new DynamicMethod($"getter-{Guid.NewGuid()}", typeof(object), [typeof(object)], ReflectionInfo.Module, true);
            var ilGen = dynamicMethod.GetILGenerator();
            ilGen.Emit(OpCodes.Ldsfld, ReflectionInfo);
            ilGen.EmitConvertToObject(ReflectionInfo.FieldType);
            ilGen.Emit(OpCodes.Ret);
            return (Func<object, object>)dynamicMethod.CreateDelegate(typeof(Func<object, object>));
        }

        protected override Action<object, object>? CreateSetter()
        {
            var dynamicMethod = new DynamicMethod($"setter-{Guid.NewGuid()}", typeof(void), [typeof(object), typeof(object)], ReflectionInfo.Module, true);
            var ilGen = dynamicMethod.GetILGenerator();
            ilGen.EmitLoadArg(1);
            ilGen.EmitConvertFromObject(ReflectionInfo.FieldType);
            ilGen.Emit(OpCodes.Stsfld, ReflectionInfo);
            ilGen.Emit(OpCodes.Ret);
            return (Action<object, object>)dynamicMethod.CreateDelegate(typeof(Action<object, object>));
        }

        public override object GetValue(object instance)
        {
            return _getter!(null!);
        }

        public override void SetValue(object instance, object? value)
        {
            _setter!(null!, value!);
        }

        public override object GetStaticValue()
        {
            return _getter!(null!);
        }

        public override void SetStaticValue(object? value)
        {
            _setter!(null!, value!);
        }
    }
}

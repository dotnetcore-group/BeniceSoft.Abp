using System.Reflection;
using System.Reflection.Emit;

namespace BeniceSoft.Core.Reflector;

public class MethodReflector : MemberReflector<MethodInfo>, IParameterReflectorProvider
{
    private readonly string _displayName;

    protected Func<object?, object?[]?, object?>? Invoker { get; }

    public override string DisplayName => _displayName;

    public ParameterReflector[] ParameterReflectors { get; }

    private MethodReflector(MethodInfo reflectionInfo) : base(reflectionInfo)
    {
        _displayName = GetDisplayName(reflectionInfo);
        Invoker = CreateInvoker();
        ParameterReflectors = reflectionInfo.GetParameters().Select(ParameterReflector.Create).ToArray();
    }

    internal static MethodReflector Create(MethodInfo reflectionInfo, CallRefOptions callOption)
    {
        ArgumentNullException.ThrowIfNull(reflectionInfo);

        return ReflectorCacheUtils<Pair<MethodInfo, CallRefOptions>, MethodReflector>.GetOrAdd(new Pair<MethodInfo, CallRefOptions>(reflectionInfo, callOption), CreateInternal);

        MethodReflector CreateInternal(Pair<MethodInfo, CallRefOptions> item)
        {
            var methodInfo = item.Item1;
            if (methodInfo.ContainsGenericParameters)
            {
                return new OpenGenericMethodReflector(methodInfo);
            }

            if (methodInfo.IsStatic)
            {
                return new StaticMethodReflector(methodInfo);
            }

            if (methodInfo.DeclaringType!.IsValueType() || callOption == CallRefOptions.Call)
            {
                return new CallMethodReflector(methodInfo);
            }

            return new MethodReflector(methodInfo);
        }
    }

    private static string GetDisplayName(MethodInfo method)
    {
        var name = $"{method.ReturnType.GetReflector().DisplayName} {method.Name}";
        if (method.IsGenericMethod)
        {
            name += "<";
            var arguments = method.GetGenericArguments();
            name += arguments[0].GetReflector().DisplayName;
            foreach (var i in 1..arguments.Length)
            {
                name += "," + arguments[i].GetReflector().DisplayName;
            }

            name += ">";
        }

        var parameterTypes = method.GetParameterTypes();
        name += "(";
        if (parameterTypes.Length == 0)
        {
            name += ")";
            return name;
        }

        name += parameterTypes[0].GetReflector().DisplayName;
        foreach (var i in 1..parameterTypes.Length)
        {
            name += "," + parameterTypes[i].GetReflector().DisplayName;
        }

        name += ")";
        return name;
    }

    protected virtual Func<object?, object?[]?, object?>? CreateInvoker()
    {
        var dynamicMethod = new DynamicMethod($"invoker_{_displayName}",
           typeof(object), [typeof(object), typeof(object[])], ReflectionInfo.Module, true);

        var ilGen = dynamicMethod.GetILGenerator();
        var parameterTypes = ReflectionInfo.GetParameterTypes();

        ilGen.EmitLoadArg(0);
        ilGen.EmitConvertFromObject(ReflectionInfo.DeclaringType!);

        if (parameterTypes.Length == 0)
        {
            return CreateDelegate();
        }

        var refParameterCount = parameterTypes.Count(x => x.IsByRef);
        if (refParameterCount == 0)
        {
            foreach (var i in parameterTypes.Length)
            {
                ilGen.EmitLoadArg(1);
                ilGen.EmitInt(i);
                ilGen.Emit(OpCodes.Ldelem_Ref);
                ilGen.EmitConvertFromObject(parameterTypes[i]);
            }

            return CreateDelegate();
        }

        var indexedLocals = new IndexedLocalBuilder[refParameterCount];
        var index = 0;
        foreach (var i in parameterTypes.Length)
        {
            ilGen.EmitLoadArg(1);
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

        return CreateDelegate(() =>
        {
            foreach (var i in indexedLocals.Length)
            {
                ilGen.EmitLoadArg(1);
                ilGen.EmitInt(indexedLocals[i].Index);
                ilGen.Emit(OpCodes.Ldloc, indexedLocals[i].LocalBuilder);
                ilGen.EmitConvertToObject(indexedLocals[i].LocalType);
                ilGen.Emit(OpCodes.Stelem_Ref);
            }
        });

        Func<object?, object?[]?, object?> CreateDelegate(Action? callback = null)
        {
            //https://github.com/dotnet/runtime/issues/67084
            ilGen.Emit(OpCodes.Callvirt, ReflectionInfo);
            //ilGen.EmitCall(OpCodes.Callvirt, ReflectionInfo, null);

            callback?.Invoke();
            if (ReflectionInfo.ReturnType == typeof(void))
            {
                ilGen.Emit(OpCodes.Ldnull);
            }
            else if (ReflectionInfo.ReturnType.IsValueType())
            {
                ilGen.EmitConvertToObject(ReflectionInfo.ReturnType);
            }

            ilGen.Emit(OpCodes.Ret);
            return (Func<object?, object?[]?, object?>)dynamicMethod.CreateDelegate(typeof(Func<object?, object?[]?, object?>));
        }
    }

    public virtual object? Invoke(object instance, params object?[]? parameters)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return Invoker!(instance, parameters);
    }

    public virtual object? StaticInvoke(params object?[]? parameters)
    {
        throw new InvalidOperationException($"Method {ReflectionInfo.Name} must be static to call this method. For invoke instance method, call 'Invoke'.");
    }

    private sealed class CallMethodReflector(MethodInfo reflectionInfo) : MethodReflector(reflectionInfo)
    {
        protected override Func<object?, object?[]?, object?>? CreateInvoker()
        {
            var dynamicMethod = new DynamicMethod($"invoker_{_displayName}",
            typeof(object), [typeof(object), typeof(object[])], ReflectionInfo.Module, true);

            var ilGen = dynamicMethod.GetILGenerator();
            var parameterTypes = ReflectionInfo.GetParameterTypes();
            var declaringType = ReflectionInfo.DeclaringType!;

            ilGen.EmitLoadArg(0);
            ilGen.EmitConvertFromObject(declaringType);
            if (declaringType.IsValueType())
            {
                var local = ilGen.DeclareLocal(declaringType);
                ilGen.Emit(OpCodes.Stloc, local);
                ilGen.Emit(OpCodes.Ldloca, local);
            }

            if (parameterTypes.Length == 0)
            {
                return CreateDelegate();
            }

            var refParameterCount = parameterTypes.Count(x => x.IsByRef);
            if (refParameterCount == 0)
            {
                foreach (var i in parameterTypes.Length)
                {
                    ilGen.EmitLoadArg(1);
                    ilGen.EmitInt(i);
                    ilGen.Emit(OpCodes.Ldelem_Ref);
                    ilGen.EmitConvertFromObject(parameterTypes[i]);
                }

                return CreateDelegate();
            }

            var indexedLocals = new IndexedLocalBuilder[refParameterCount];
            var index = 0;
            foreach (var i in parameterTypes.Length)
            {
                ilGen.EmitLoadArg(1);
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

            return CreateDelegate(() =>
            {
                foreach (var i in indexedLocals.Length)
                {
                    ilGen.EmitLoadArg(1);
                    ilGen.EmitInt(indexedLocals[i].Index);
                    ilGen.Emit(OpCodes.Ldloc, indexedLocals[i].LocalBuilder);
                    ilGen.EmitConvertToObject(indexedLocals[i].LocalType);
                    ilGen.Emit(OpCodes.Stelem_Ref);
                }
            });

            Func<object?, object?[]?, object?> CreateDelegate(Action? callback = null)
            {
                ilGen.EmitCall(OpCodes.Call, ReflectionInfo, null);
                callback?.Invoke();
                if (ReflectionInfo.ReturnType == typeof(void))
                {
                    ilGen.Emit(OpCodes.Ldnull);
                }
                else if (ReflectionInfo.ReturnType.IsValueType())
                {
                    ilGen.EmitConvertToObject(ReflectionInfo.ReturnType);
                }

                ilGen.Emit(OpCodes.Ret);
                return (Func<object?, object?[]?, object?>)dynamicMethod.CreateDelegate(typeof(Func<object?, object?[]?, object?>));
            }
        }
    }

    private sealed class OpenGenericMethodReflector(MethodInfo reflectionInfo) : MethodReflector(reflectionInfo)
    {
        protected override Func<object?, object?[]?, object?>? CreateInvoker()
        {
            return null;
        }

        public override object? Invoke(object instance, params object?[]? parameters)
        {
            throw new InvalidOperationException("Late bound operations cannot be performed on types or methods for which ContainsGenericParameters is true.");
        }

        public override object? StaticInvoke(params object?[]? parameters)
        {
            throw new InvalidOperationException("Late bound operations cannot be performed on types or methods for which ContainsGenericParameters is true.");
        }
    }

    private sealed class StaticMethodReflector(MethodInfo reflectionInfo) : MethodReflector(reflectionInfo)
    {
        protected override Func<object?, object?[]?, object?>? CreateInvoker()
        {
            var dynamicMethod = new DynamicMethod($"invoker_{_displayName}",
           typeof(object), [typeof(object), typeof(object[])], ReflectionInfo.Module, true);

            var ilGen = dynamicMethod.GetILGenerator();
            var parameterTypes = ReflectionInfo.GetParameterTypes();

            if (parameterTypes.Length == 0)
            {
                return CreateDelegate();
            }

            var refParameterCount = parameterTypes.Count(x => x.IsByRef);
            if (refParameterCount == 0)
            {
                foreach (var i in parameterTypes.Length)
                {
                    ilGen.EmitLoadArg(1);
                    ilGen.EmitInt(i);
                    ilGen.Emit(OpCodes.Ldelem_Ref);
                    ilGen.EmitConvertFromObject(parameterTypes[i]);
                }

                return CreateDelegate();
            }

            var indexedLocals = new IndexedLocalBuilder[refParameterCount];
            var index = 0;
            foreach (var i in parameterTypes.Length)
            {
                ilGen.EmitLoadArg(1);
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

            return CreateDelegate(() =>
            {
                foreach (var i in indexedLocals.Length)
                {
                    ilGen.EmitLoadArg(1);
                    ilGen.EmitInt(indexedLocals[i].Index);
                    ilGen.Emit(OpCodes.Ldloc, indexedLocals[i].LocalBuilder);
                    ilGen.EmitConvertToObject(indexedLocals[i].LocalType);
                    ilGen.Emit(OpCodes.Stelem_Ref);
                }
            });

            Func<object?, object?[]?, object?> CreateDelegate(Action? callback = null)
            {
                ilGen.EmitCall(OpCodes.Call, ReflectionInfo, null);
                callback?.Invoke();
                if (ReflectionInfo.ReturnType == typeof(void))
                {
                    ilGen.Emit(OpCodes.Ldnull);
                }
                else if (ReflectionInfo.ReturnType.IsValueType())
                {
                    ilGen.EmitConvertToObject(ReflectionInfo.ReturnType);
                }

                ilGen.Emit(OpCodes.Ret);
                return (Func<object?, object?[]?, object?>)dynamicMethod.CreateDelegate(typeof(Func<object?, object?[]?, object?>));
            }
        }

        public override object? Invoke(object instance, params object?[]? parameters)
        {
            return Invoker!(null, parameters);
        }

        public override object? StaticInvoke(params object?[]? parameters)
        {
            return Invoker!(null, parameters);
        }
    }
}

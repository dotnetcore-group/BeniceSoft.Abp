using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using BeniceSoft.Core;
using Volo.Abp;
using Volo.Abp.DynamicProxy;

namespace BeniceSoft.Abp.Extensions.Caching.Extensions;

public static class AbpMethodInvocationExtensions
{
    private static readonly ConcurrentDictionary<Type, Func<object, object>> ResultFuncCache = new();

    private static readonly ConcurrentDictionary<Type, Func<object, Task>> AsTaskFuncCache = new();

    public static async Task<T?> UnwrapAsyncReturnValue<T>(this IAbpMethodInvocation invocation)
    {
        return (T?)await UnwrapAsyncReturnValue(invocation);
    }

    public static async Task<object?> UnwrapAsyncReturnValue(this IAbpMethodInvocation invocation)
    {
        if (invocation == null)
        {
            throw new ArgumentNullException(nameof(invocation));
        }

        if (!invocation.IsAsync())
        {
            throw new AbpException("This operation only support asynchronous method.");
        }

        var returnValue = invocation.ReturnValue;
        if (returnValue == null)
        {
            return Task.FromResult<object?>(default);
        }

        var returnType = returnValue.GetType();
        return await Unwrap(returnValue, returnType);
    }

    public static bool IsAsync(this IAbpMethodInvocation invocation)
    {
        return IsAsyncType(invocation.Method.ReturnType);
    }

    private static async Task<object?> Unwrap(object value, Type valueType)
    {
        object result;

        if (valueType.IsTaskWithVoidTaskResult())
        {
            return default;
        }

        if (valueType.IsTaskWithResult())
        {
            // NOTE: we can not use "result = (object)(await (dynamic)value)" here,
            // because when T of Task<T> is non-public, we will get a RuntimeBinderException that says "Cannot implicitly convert type 'void' to 'object'".
            await (Task)value;
            result = GetTaskResult(value, valueType);
        }
        else if (valueType.IsValueTaskWithResult())
        {
            // NOTE: we can not use "result = (object)(await (dynamic)value)" here,
            // because when T of ValueTask<T> is non-public, we will get a RuntimeBinderException that says "'System.ValueType' does not contain a definition for 'GetAwaiter'".
            await ValueTaskWithResultToTask(value, valueType);
            result = GetTaskResult(value, valueType);
        }
        else if (value is Task)
        {
            return null;
        }
        else if (value is ValueTask)
        {
            return null;
        }
        else
        {
            result = value;
        }

        if (result == null)
        {
            return null;
        }

        var resultType = result.GetType();
        if (IsAsyncType(resultType))
        {
            return await Unwrap(result, resultType);
        }

        return result;
    }

    // mark this method as "internal" for testing.
    internal static Func<object, object> CreateFuncToGetTaskResult(Type type)
    {
        var parameter = Expression.Parameter(typeof(object), "type");
        var convertedParameter = Expression.Convert(parameter, type);
        var property = Expression.Property(convertedParameter, nameof(Task<int>.Result));
        var convertedProperty = Expression.Convert(property, typeof(object));
        var exp = Expression.Lambda<Func<object, object>>(convertedProperty, parameter);
        return exp.Compile();
    }

    // value should be ValueTask<T>
    private static Task ValueTaskWithResultToTask(object value, Type valueType)
    {
        // NOTE: if we use "await (dynamic)value" to await a ValueTask<T> when T is non-public, we will get an RuntimeBinderException that says
        // 'System.ValueType' does not contain a definition for 'GetAwaiter'.
        // So we have to convert ValueTask<T> to Task and then await it.
        // Please fix this logic if there is a better solution.
        var func = AsTaskFuncCache.GetOrAdd(valueType, k =>
        {
            var parameter = Expression.Parameter(typeof(object), "type");
            var convertedParameter = Expression.Convert(parameter, k);
            var method = k.GetMethod(nameof(ValueTask<int>.AsTask))!;
            var property = Expression.Call(convertedParameter, method);
            var convertedProperty = Expression.Convert(property, typeof(Task));
            var exp = Expression.Lambda<Func<object, Task>>(convertedProperty, parameter);
            return exp.Compile();
        });
        return func(value);
    }

    // value should be Task<T> or ValueTask<T>
    private static object GetTaskResult(object value, Type valueType)
    {
        // There are several ways to get the value of "Result" of Task<T> or ValueTask<T> after it is awaited.
        // The Benchmark can be viewed in GetTaskResultBenchmarks.cs in AspectCore.Core.Benchmark.
        // Here is a test result that can be referred to:
        /*
            |                            Method |         Mean |
            |---------------------------------- |-------------:|
            |          GetTaskResult_Reflection |     338.6 ns |
            | GetTaskResult_ReflectionWithCache |     284.9 ns |
            |          GetTaskResult_Expression | 224,786.1 ns |
            | GetTaskResult_ExpressionWithCache |     126.2 ns |
            |        GetTaskResult_AwaitDynamic |     117.1 ns |
        */
        // So we use "ExpressionWithCache" here.
        // Please fix this logic if there is a better solution.
        var func = ResultFuncCache.GetOrAdd(valueType, k => CreateFuncToGetTaskResult(k));
        return func(value);
    }


    private static bool IsAsyncType(Type type)
    {
        return type.IsTask() || type.IsTaskWithResult() || type.IsValueTask() || type.IsValueTaskWithResult();
    }
}
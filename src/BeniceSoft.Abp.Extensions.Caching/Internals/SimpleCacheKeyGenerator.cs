using BeniceSoft.Abp.Extensions.Caching.Abstractions.Interfaces;
using BeniceSoft.Abp.Extensions.Caching.Configurations;
using BeniceSoft.Core;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Volo.Abp.DynamicProxy;

namespace BeniceSoft.Abp.Extensions.Caching.Internals;

internal class SimpleCacheKeyGenerator : ICacheKeyGenerator
{
    private static readonly JsonSerializerOptions KeySerializerOptions = new()
    {
        IncludeFields = true
    };

    public string Generate(IAbpMethodInvocation invocation, string? key)
    {
        var methodInfo = invocation.Method;
        var cacheKey = new StringBuilder();
        if (!BeniceSoftCachingConfiguration.Instance.CacheKeyPrefix.IsNullOrWhiteSpace())
        {
            cacheKey.Append(BeniceSoftCachingConfiguration.Instance.CacheKeyPrefix);
        }

        cacheKey.AppendJoin(".",
            methodInfo.DeclaringType?.Namespace,
            methodInfo.DeclaringType?.Name,
            methodInfo.Name);

        var methodParameters = methodInfo.GetParameters();

        // 未设置表达式时，按方法签名和实际参数值生成摘要，避免不同参数共用缓存
        if (string.IsNullOrWhiteSpace(key))
        {
            if (methodParameters.Length > 0 || methodInfo.IsGenericMethod || methodInfo.DeclaringType?.IsGenericType == true)
            {
                var arguments = methodParameters
                    .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
                    .Select(parameter =>
                    {
                        var value = invocation.Arguments[parameter.Position];
                        return new
                        {
                            Type = value?.GetType().AssemblyQualifiedName,
                            Value = value
                        };
                    }).ToArray();
                var bytes = JsonUtils.SerializeBytes(new
                {
                    DeclaringType = methodInfo.DeclaringType?.AssemblyQualifiedName,
                    ParameterTypes = methodParameters.Select(parameter => parameter.ParameterType.AssemblyQualifiedName).ToArray(),
                    GenericArguments = methodInfo.GetGenericArguments().Select(type => type.AssemblyQualifiedName).ToArray(),
                    Arguments = arguments
                }, KeySerializerOptions);

                cacheKey.Append(":");
                cacheKey.Append(Convert.ToHexString(SHA256.HashData(bytes)));
            }
        }
        else
        {
            cacheKey.Append(":");

            var parameters = invocation
                .ArgumentsDictionary
                .Select(x => Expression.Parameter(methodParameters.Single(p => p.Name == x.Key).ParameterType, x.Key))
                .ToArray();
            var lambda = DynamicExpressionParser.ParseLambda(parameters, typeof(string), key);
            var value = lambda.Compile().DynamicInvoke(invocation.Arguments);
            cacheKey.Append(value);
        }

        return cacheKey.ToString();
    }
}

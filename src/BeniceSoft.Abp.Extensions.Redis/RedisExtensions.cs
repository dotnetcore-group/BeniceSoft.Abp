using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// Redis 扩展方法
/// </summary>
public static class RedisExtensions
{
    /// <summary>
    /// 添加 Redis 连接
    /// </summary>
    public static IServiceCollection AddRedisConnection(
        this IServiceCollection services,
        string connectionString,
        object? serviceKey = null)
    {
        services.TryAddKeyedSingleton<IRedisConnection>(serviceKey, (sp, _) =>
        {
            var logger = sp.GetService<ILogger<RedisConnection>>();
            return new RedisConnection(connectionString, logger);
        });
        return services;
    }

    /// <summary>
    /// 添加 Redis 连接
    /// </summary>
    public static IServiceCollection AddRedisConnection(
        this IServiceCollection services,
        ConfigurationOptions options,
        object? serviceKey = null)
    {
        services.TryAddKeyedSingleton<IRedisConnection>(serviceKey, (sp, _) =>
        {
            var logger = sp.GetService<ILogger<RedisConnection>>();
            return new RedisConnection(options, logger);
        });
        return services;
    }

    /// <summary>
    /// 添加 Redis 客户端
    /// </summary>
    public static IServiceCollection AddRedisClient(
        this IServiceCollection services,
        int dbIndex = -1,
        object? serviceKey = null,
        object? connectionKey = null)
    {
        services.TryAddKeyedTransient(serviceKey, (sp, _) =>
        {
            var connection = connectionKey == null
                ? sp.GetRequiredService<IRedisConnection>()
                : sp.GetRequiredKeyedService<IRedisConnection>(connectionKey);
            var logger = sp.GetService<ILogger<RedisClient>>();
            return new RedisClient(connection, dbIndex, logger);
        });
        return services;
    }
}


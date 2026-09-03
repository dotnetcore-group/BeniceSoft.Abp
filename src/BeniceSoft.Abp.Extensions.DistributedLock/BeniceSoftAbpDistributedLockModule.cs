using BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;
using BeniceSoft.Abp.Extensions.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.Extensions.DistributedLock;

public class BeniceSoftAbpDistributedLockModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.OnRegistered(DistributedLockInterceptorRegistrar.RegisterIfNeeded);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var section = configuration.GetSection("DistributedLock");
        context.Services.Configure<DistributedLockOptions>(section);

        var connectionString = section[nameof(DistributedLockOptions.ConnectionString)];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new AbpInitializationException(
                "DistributedLock:ConnectionString 未配置");
        }

        // 分布式锁使用独立的redisclient ，不依赖 BeniceSoftAbpRedisModule 模块的注入， 
        // 为了避免各个业务模块定义不同的dbindex导致的问题，默认内置 0 
        var serviceKey = "BeniceSoft_DistributedLock";
        context.Services.AddRedisConnection(connectionString, serviceKey);
        context.Services.TryAddKeyedSingleton(serviceKey, (sp, _) =>
        {
            var connection = sp.GetRequiredKeyedService<IRedisConnection>(serviceKey);
            var logger = sp.GetService<ILogger<RedisClient>>();
            return new RedisClient(connection, 0, logger);
        });

        context.Services.AddSingleton(sp =>
        {
            var redisClient = sp.GetRequiredKeyedService<RedisClient>(serviceKey);
            var logger = sp.GetRequiredService<ILogger<BeniceSoftDistributedLockProvider>>();
            var redisLockLogger = sp.GetService<ILogger<RedisLock>>();

            return new BeniceSoftDistributedLockProvider([redisClient], logger, redisLockLogger);
        });

        context.Services.AddSingleton<IDistributedLockProvider>(sp =>
            sp.GetRequiredService<BeniceSoftDistributedLockProvider>());
    }
}

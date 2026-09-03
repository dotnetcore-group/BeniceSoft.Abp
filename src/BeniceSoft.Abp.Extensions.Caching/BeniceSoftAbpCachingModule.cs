using BeniceSoft.Abp.Extensions.Caching.Configurations;
using BeniceSoft.Abp.Extensions.Caching.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Caching;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.Extensions.Caching;

[DependsOn(
    typeof(AbpCachingModule))]
public class BeniceSoftAbpCachingModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.OnRegistered(CacheableInterceptorRegistrar.RegisterIfNeeded);

        var configuration = context.Services.GetConfiguration();
        var section = configuration.GetSection("BeniceSoft:Caching");

        // 手动读取配置值
        var cacheKeyPrefix = section["CacheKeyPrefix"];
        var defaultExpirationSeconds = section["DefaultExpirationSeconds"];

        if (!string.IsNullOrWhiteSpace(cacheKeyPrefix))
        {
            BeniceSoftCachingConfiguration.Instance.CacheKeyPrefix = cacheKeyPrefix;
        }

        if (!string.IsNullOrWhiteSpace(defaultExpirationSeconds) &&
            int.TryParse(defaultExpirationSeconds, out var seconds))
        {
            BeniceSoftCachingConfiguration.Instance.DefaultExpirationSeconds = seconds;
        }
    }
}
using BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;
using BeniceSoft.Abp.Extensions.Redis;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.Extensions.RateLimiting;

/// <summary>
/// 速率限流模块
/// </summary>
[DependsOn(typeof(BeniceSoftAbpRedisModule))]
public class BeniceSoftAbpRateLimitingModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // 注册拦截器
        context.Services.OnRegistered(RateLimitInterceptorRegistrar.RegisterIfNeeded);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 读取配置
        var section = context.Services.GetConfiguration().GetSection("RateLimiting");
        context.Services.Configure<RateLimitOptions>(section);

        // 注册 HttpContextAccessor（如果尚未注册）
        context.Services.AddHttpContextAccessor();
    }
}


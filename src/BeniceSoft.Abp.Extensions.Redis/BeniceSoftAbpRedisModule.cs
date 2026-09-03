using BeniceSoft.Abp.Core;
using BeniceSoft.Core;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// BeniceSoft ABP Redis 模块
/// </summary>
[DependsOn(
    typeof(BeniceSoftAbpCoreModule)
)]
public class BeniceSoftAbpRedisModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        var connectionString = configuration["Redis:Configuration"];
        var dbIndex = configuration["Redis:DbIndex"].ToInt32();
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            context.Services.AddRedisConnection(connectionString);
            context.Services.AddRedisClient(dbIndex);
        }
    }
}


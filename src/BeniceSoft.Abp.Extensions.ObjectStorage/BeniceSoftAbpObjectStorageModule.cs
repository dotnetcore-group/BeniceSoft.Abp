using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Volo.Abp.Timing;

namespace BeniceSoft.Abp.Extensions.ObjectStorage;

[DependsOn(typeof(AbpTimingModule))]
public class BeniceSoftAbpObjectStorageModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        context.Services.Configure<ObjectStorageOptions>(configuration.GetSection(ObjectStorageOptions.Section));

        context.Services.AddSingleton<IObjectStorageProvider, AliyunOssStorageProvider>();
        context.Services.AddSingleton<IObjectStorageProvider, HuaweiObsStorageProvider>();
        context.Services.AddSingleton<IObjectStorageProvider, LocalStorageProvider>();
        context.Services.AddSingleton<IObjectStorageProvider, MinioStorageProvider>();
        context.Services.AddSingleton<IObjectStorageResolver, ObjectStorageResolver>();
    }
}

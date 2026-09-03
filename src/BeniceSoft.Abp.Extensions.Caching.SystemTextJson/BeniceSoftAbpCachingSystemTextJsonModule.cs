using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.Extensions.Caching.SystemTextJson;

[DependsOn(typeof(BeniceSoftAbpCachingModule))]
public class BeniceSoftAbpCachingSystemTextJsonModule : AbpModule
{
}
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.Extensions.Caching.MessagePack;

[DependsOn(typeof(BeniceSoftAbpCachingModule))]
public class BeniceSoftAbpCachingMessagePackModule : AbpModule
{
}
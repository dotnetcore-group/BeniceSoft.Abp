using Volo.Abp.EventBus;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.EventBus.Dtm;

[DependsOn(
    typeof(AbpEventBusModule)
)]
public class BeniceSoftAbpEventBusDtmModule : AbpModule
{
}

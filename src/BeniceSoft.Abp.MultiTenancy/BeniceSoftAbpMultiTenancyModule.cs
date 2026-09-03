using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;

namespace BeniceSoft.Abp.MultiTenancy;

[DependsOn(typeof(AbpMultiTenancyModule))]
public class BeniceSoftAbpMultiTenancyModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpTenantResolveOptions>(options =>
        {
            options.TenantResolvers.Insert(0, new BeniceSoftCurrentUserTenantResolveContributor());
        });
    }
}

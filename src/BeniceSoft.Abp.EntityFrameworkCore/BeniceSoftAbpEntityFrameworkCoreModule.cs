using BeniceSoft.Abp.Ddd.Domain;
using BeniceSoft.Abp.Ddd.Domain.Entity;
using BeniceSoft.Abp.Extensions.DynamicQuery.EfCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.EntityFrameworkCore;

[DependsOn(
    typeof(AbpEntityFrameworkCoreModule),
    typeof(BeniceSoftAbpDddDomainModule),
    typeof(BeniceSoftAbpDynamicQueryEfCoreModule)
)]
public class BeniceSoftAbpEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDataFilterOptions>(options =>
        {
            options.DefaultStates[typeof(IHaveClientId)] = new DataFilterState(isEnabled: true);
        });
    }
}
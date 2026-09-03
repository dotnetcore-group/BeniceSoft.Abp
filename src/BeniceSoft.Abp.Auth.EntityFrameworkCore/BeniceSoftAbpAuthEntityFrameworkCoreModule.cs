using BeniceSoft.Abp.Auth.EntityFrameworkCore.Interceptors;
using BeniceSoft.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore;

[DependsOn(
    typeof(BeniceSoftAbpEntityFrameworkCoreModule)
)]
public class BeniceSoftAbpAuthEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDbContextOptions>(options =>
        {
            options.PreConfigure(ctx =>
            {
                ctx.DbContextOptions.AddFieldPermissionInterceptor(ctx.ServiceProvider);
            });
        });
    }
}

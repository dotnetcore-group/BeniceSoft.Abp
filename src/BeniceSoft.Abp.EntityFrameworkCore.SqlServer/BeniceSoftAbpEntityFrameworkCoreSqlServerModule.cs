using Volo.Abp.EntityFrameworkCore.SqlServer;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.EntityFrameworkCore.SqlServer;

[DependsOn(
    typeof(BeniceSoftAbpEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqlServerModule)
)]
public class BeniceSoftAbpEntityFrameworkCoreSqlServerModule : AbpModule
{
}

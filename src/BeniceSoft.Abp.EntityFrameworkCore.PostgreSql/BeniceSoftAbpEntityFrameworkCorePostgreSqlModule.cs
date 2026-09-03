using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.EntityFrameworkCore.PostgreSql;

[DependsOn(
    typeof(BeniceSoftAbpEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule)
)]
public class BeniceSoftAbpEntityFrameworkCorePostgreSqlModule : AbpModule
{
}

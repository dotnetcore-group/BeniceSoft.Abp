using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 分库分表模块
/// </summary>
[DependsOn(typeof(BeniceSoftAbpEntityFrameworkCoreModule))]
public class BeniceSoftAbpEntityFrameworkCoreShardingModule : AbpModule
{
}

using Volo.Abp.Modularity;
using Wecharmer.AM;
using Wecharmer.PermissionCenter;
using Wecharmer.WorkflowCenter;

namespace BeniceSoft.Abp.Sample.RemoteService.Implements;

[DependsOn(
    typeof(AmSdkModule),
    typeof(PermissionCenterSdkModule),
    typeof(WorkflowCenterSdkModule)
)]
public class RemoteServiceModule : AbpModule
{
}

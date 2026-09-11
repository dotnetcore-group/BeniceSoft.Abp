using Volo.Abp.Modularity;

using Wecharmer.PermissionCenter;


namespace BeniceSoft.Abp.Sample.RemoteService.Implements;

[DependsOn(
    //typeof(AmSdkModule),
    typeof(PermissionCenterSdkModule)
    //typeof(WorkflowCenterSdkModule)
)]
public class RemoteServiceModule : AbpModule
{
}

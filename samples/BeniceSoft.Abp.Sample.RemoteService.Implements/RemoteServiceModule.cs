using Volo.Abp.Modularity;
using Wecharmer.FileCenter;
using Wecharmer.PermissionCenter;

namespace BeniceSoft.Abp.Sample.RemoteService.Implements;

[DependsOn(
    typeof(PermissionCenterSdkModule),
    typeof(FileCenterSdkModule)
)]
public class RemoteServiceModule : AbpModule
{
}

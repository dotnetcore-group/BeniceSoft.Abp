using BeniceSoft.Abp.Core;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.Http.Client;

/// <summary>
/// abp http client proxy
/// </summary>
[DependsOn(
    typeof(BeniceSoftAbpCoreModule),
    typeof(AbpHttpClientModule)
)]
public class BeniceSoftAbpHttpClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 机器身份 ClientId/Secret 来自 BeniceSoftAuthOptions（Auth 节），由 AddBeniceSoftAuthentication 注册
        context.Services.AddHttpClient(MachineAccessTokenProvider.HttpClientName);
    }
}

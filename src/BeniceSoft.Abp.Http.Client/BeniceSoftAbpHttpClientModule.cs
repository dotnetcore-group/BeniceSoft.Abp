using BeniceSoft.Abp.Core;
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
}

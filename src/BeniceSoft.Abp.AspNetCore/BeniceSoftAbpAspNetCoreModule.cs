using BeniceSoft.Abp.AspNetCore.Filters;
using BeniceSoft.Abp.AspNetCore.Localizations;
using BeniceSoft.Abp.MultiTenancy;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.ExceptionHandling;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.AspNetCore;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreMvcModule),
    typeof(AbpAspNetCoreMultiTenancyModule),
    typeof(BeniceSoftAbpMultiTenancyModule)
)]
public class BeniceSoftAbpAspNetCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<MvcOptions>(options =>
        {
            // 移除 abp的错误拦截器
            var filterMetadata = options.Filters.FirstOrDefault(x =>
                x is ServiceFilterAttribute attribute && attribute.ServiceType == typeof(AbpExceptionFilter));
            if (filterMetadata is not null)
            {
                options.Filters.Remove(filterMetadata);
            }

            // 统一响应格式化
            options.Filters.Add<JsonFormatResponseFilter>();
        });

        Configure<BeniceSoftCultureMapOptions>(options =>
        {
            var zhHansCultureMapInfo = new CultureMapInfo
            {
                TargetCulture = "zh-Hans",
                SourceCultures =
                [
                    "zh", "zh_cn", "zh-CN"
                ]
            };
            options.CulturesMaps.Add(zhHansCultureMapInfo);
            options.UiCulturesMaps.Add(zhHansCultureMapInfo);

            var enUsCultureMapInfo = new CultureMapInfo
            {
                TargetCulture = "en-US",
                SourceCultures =
                [
                    "en", "en_us", "en-US"
                ]
            };
            options.CulturesMaps.Add(enUsCultureMapInfo);
            options.UiCulturesMaps.Add(enUsCultureMapInfo);
        });
    }
}
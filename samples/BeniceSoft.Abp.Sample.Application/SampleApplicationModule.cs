using BeniceSoft.Abp.Ddd.Application;
using BeniceSoft.Abp.OperationLogging;
using BeniceSoft.Abp.OperationLogging.Abstractions;
using BeniceSoft.Abp.Sample.Application.Contracts;
using BeniceSoft.Office.Pdf;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.Sample.Application;

[DependsOn(
    typeof(BeniceSoftAbpDddApplicationModule),
    typeof(BeniceSoftAbpOperationLoggingModule),
    typeof(SampleApplicationContractsModule)
)]
public class SampleApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<BeniceSoftOperationLogOptions>(options =>
        {
            options.ServiceName = "Sample";
        });

        context.Services.AddSingleton<IPdfParser, PdfParser>();
    }
}

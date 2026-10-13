using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.Extensions.OpenTelemetry;

/// <summary>
/// OpenTelemetry 模块：注册 Tracing / Metrics
/// </summary>
public class BeniceSoftAbpOpenTelemetryModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        if (!BeniceSoftOpenTelemetryEnvironment.IsOtlpEndpointConfigured)
        {
            return;
        }

        var serviceName = BeniceSoftOpenTelemetryEnvironment.GetServiceName(context.Services.GetApplicationName());

        context.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource("Npgsql"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .UseOtlpExporter();
    }
}
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace BeniceSoft.Abp.AspNetCore.Extensions;

public static class ServiceDiscoveryEndpointExtensions
{
    /// <summary>
    /// 配置健康检查终点地址
    /// </summary>
    /// <param name="endpoints"></param>
    /// <returns></returns>
    public static IEndpointRouteBuilder MapServiceDiscoveryHealthCheck(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", async context =>
        {
            var serviceName = "unknown";
            var version = "1.0.0";
            var environment = "Production";

            try
            {
                var hostEnvironment = context.RequestServices.GetService<IHostEnvironment>();
                if (hostEnvironment != null)
                {
                    environment = hostEnvironment.EnvironmentName;
                }

                var entryAssembly = Assembly.GetEntryAssembly();
                if (entryAssembly != null)
                {
                    version = entryAssembly.GetName().Version?.ToString(3) ?? "1.0.0";
                }

                var optionsType = Type.GetType("BeniceSoft.Abp.ServiceDiscovery.ServiceRegistryOptions, BeniceSoft.Abp.ServiceDiscovery");
                if (optionsType != null)
                {
                    var optionsServiceType = typeof(Microsoft.Extensions.Options.IOptions<>).MakeGenericType(optionsType);
                    var optionsService = context.RequestServices.GetService(optionsServiceType);
                    if (optionsService != null)
                    {
                        var valueProperty = optionsServiceType.GetProperty("Value");
                        var options = valueProperty?.GetValue(optionsService);
                        if (options != null)
                        {
                            var serviceNameProperty = optionsType.GetProperty("ServiceName");
                            var serviceNameValue = serviceNameProperty?.GetValue(options) as string;
                            if (!string.IsNullOrEmpty(serviceNameValue))
                            {
                                serviceName = serviceNameValue;
                            }

                            var metadataProperty = optionsType.GetProperty("Metadata");
                            var metadata = metadataProperty?.GetValue(options);
                            if (metadata != null)
                            {
                                var versionProperty = metadata.GetType().GetProperty("Version");
                                var versionValue = versionProperty?.GetValue(metadata) as string;
                                if (!string.IsNullOrEmpty(versionValue))
                                {
                                    version = versionValue;
                                }

                                // 优先从 Metadata.Environment 读取环境信息
                                var environmentProperty = metadata.GetType().GetProperty("Environment");
                                var environmentValue = environmentProperty?.GetValue(metadata) as string;
                                if (!string.IsNullOrEmpty(environmentValue))
                                {
                                    environment = environmentValue;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            await context.Response.WriteAsJsonAsync(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                service = serviceName,
                version = version,
                environment = environment
            });
        });

        return endpoints;
    }
}


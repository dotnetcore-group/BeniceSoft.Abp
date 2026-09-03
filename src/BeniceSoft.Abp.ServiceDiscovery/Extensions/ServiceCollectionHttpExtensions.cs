using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BeniceSoft.Abp.ServiceDiscovery;

public static class ServiceCollectionHttpExtensions
{
    public static IServiceCollection AddHttpServiceDiscovery(
        this IServiceCollection services,
        Action<ServiceRegistryOptions> configure)
    {
        services.Configure(configure);

        services.AddOptions<ServiceRegistryOptions>()
            .PostConfigure<IHostEnvironment>((options, environment) =>
            {
                if (environment != null && string.IsNullOrEmpty(options.Metadata.Environment))
                {
                    options.Metadata.Environment = environment.EnvironmentName;
                }

                if (string.IsNullOrEmpty(options.Metadata.Version))
                {
                    var entryAssembly = Assembly.GetEntryAssembly();
                    var version = entryAssembly?.GetName().Version?.ToString(3) ?? "1.0.0";
                    options.Metadata.Version = version;
                }
            });

        services.AddOptions<ServiceRegistryOptions>()
            .Validate(options =>
            {
                if (string.IsNullOrEmpty(options.GatewayBaseUrl))
                {
                    return false;
                }
                return true;
            }, "GatewayBaseUrl is required for HTTP service discovery.");

        services.AddHttpClient("ServiceDiscovery", (sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("User-Agent", "ServiceDiscovery-Client");
        });

        services.AddSingleton<IServiceRegistry, HttpServiceRegistry>();

        services.AddHostedService<ServiceRegistrationHostedService>();

        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;

namespace BeniceSoft.Http.FluentClient;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register a singleton IFluentClient backed by IHttpClientFactory.
    /// Use <paramref name="configureHttpClient"/> to add DelegatingHandlers (e.g. auth), configure timeouts, etc.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseUrl">The base URL for all requests made through this client.</param>
    /// <param name="configureHttpClient">Optional callback to configure the underlying IHttpClientBuilder.</param>
    /// <param name="configureFluentClient">Optional callback to configure the IFluentClient instance (e.g. add Filters, set serializer).</param>
    public static IServiceCollection AddFluentClient(
        this IServiceCollection services,
        string baseUrl,
        Action<IHttpClientBuilder>? configureHttpClient = null,
        Action<IFluentClient>? configureFluentClient = null)
    {
        var builder = services.AddHttpClient("FluentClient");
        configureHttpClient?.Invoke(builder);

        services.AddSingleton<IFluentClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient("FluentClient");
            var client = new FluentClient(httpClient, baseUrl, manageBaseClient: false);
            configureFluentClient?.Invoke(client);
            return client;
        });

        return services;
    }
}

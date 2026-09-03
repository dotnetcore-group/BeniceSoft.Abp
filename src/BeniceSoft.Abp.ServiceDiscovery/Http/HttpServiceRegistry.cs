using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BeniceSoft.Abp.ServiceDiscovery;

/// <summary>
/// 基于 HTTP 的服务注册实现
/// </summary>
public class HttpServiceRegistry : IServiceRegistry
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpServiceRegistry> _logger;
    private readonly string _gatewayBaseUrl;
    private const string HttpClientName = "ServiceDiscovery";

    public HttpServiceRegistry(
        IHttpClientFactory httpClientFactory,
        ILogger<HttpServiceRegistry> logger,
        IOptions<ServiceRegistryOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _gatewayBaseUrl = options.Value.GatewayBaseUrl?.TrimEnd('/')
            ?? throw new ArgumentNullException(nameof(options.Value.GatewayBaseUrl));
    }

    private HttpClient CreateHttpClient()
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(_gatewayBaseUrl);
        return client;
    }

    public async Task RegisterAsync(ServiceInstance instance)
    {
        using var client = CreateHttpClient();
        var response = await client.PostAsJsonAsync(
            "api/service-discovery/register",
            instance);

        response.EnsureSuccessStatusCode();

        _logger.LogInformation(
            "Service registered: {ServiceName} at {Address}",
            instance.ServiceName, instance.Address);
    }

    public async Task DeregisterAsync(string serviceName, string address)
    {
        try
        {
            using var client = CreateHttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Delete, "api/service-discovery/deregister")
            {
                Content = JsonContent.Create(new { ServiceName = serviceName, Address = address })
            };
            var response = await client.SendAsync(request);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to deregister service: {ServiceName}", serviceName);
        }
    }
}


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
        try
        {
            using var response = await client.PostAsJsonAsync(
                "api/service-discovery/register",
                instance);

            if (!response.IsSuccessStatusCode)
            {
                throw new ServiceRegistryException(
                    $"网关 Admin {_gatewayBaseUrl} 拒绝服务 {instance.ServiceName} 注册，HTTP {(int)response.StatusCode}");
            }
        }
        catch (ServiceRegistryException)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        {
            throw GatewayUnreachable(instance.ServiceName, "无法连接", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw GatewayUnreachable(instance.ServiceName, "连接超时", ex);
        }

        _logger.LogInformation(
            "Service registered successfully: {ServiceName} at {Address}",
            instance.ServiceName, instance.Address);
    }

    private ServiceRegistryException GatewayUnreachable(string serviceName, string reason, Exception inner)
    {
        return new ServiceRegistryException(
            $"{reason}网关 Admin {_gatewayBaseUrl}，服务 {serviceName} 注册失败，请确认 Gateway Admin 已启动",
            inner);
    }
}


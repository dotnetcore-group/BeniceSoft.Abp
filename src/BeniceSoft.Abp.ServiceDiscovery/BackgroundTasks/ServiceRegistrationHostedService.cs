using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BeniceSoft.Abp.ServiceDiscovery;

public class ServiceRegistrationHostedService : BackgroundService
{
    private readonly IServiceRegistry _serviceRegistry;
    private readonly ServiceRegistryOptions _options;
    private readonly ILogger<ServiceRegistrationHostedService> _logger;
    private readonly IConfiguration _configuration;
    private string? _address;
    private string? _lastRegistrationError;
    private volatile bool _isRegistered;

    public ServiceRegistrationHostedService(
        IServiceRegistry serviceRegistry,
        IOptions<ServiceRegistryOptions> options,
        ILogger<ServiceRegistrationHostedService> logger,
        IConfiguration configuration)
    {
        _serviceRegistry = serviceRegistry;
        _options = options.Value;
        _logger = logger;
        _configuration = configuration;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableAutoRegistration)
        {
            _logger.LogDebug("Service discovery auto registration is disabled");
            return;
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableAutoRegistration)
        {
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        var retryCount = 0;
        while (!_isRegistered && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await TryRegisterAsync())
                {
                    _isRegistered = true;
                    break;
                }

                retryCount++;
                if (_options.BlockStartupOnRegistrationFailure && retryCount >= _options.MaxStartupRetries)
                {
                    throw new ServiceRegistryException(
                        _lastRegistrationError
                        ?? $"服务 {_options.ServiceName} 注册失败，已重试 {retryCount} 次");
                }

                await Task.Delay(_options.RegistrationRetryInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "服务 {ServiceName} 注册失败，地址 {Address}",
                    _options.ServiceName, _address);

                if (_options.BlockStartupOnRegistrationFailure)
                {
                    throw;
                }

                await Task.Delay(_options.RegistrationRetryInterval, stoppingToken);
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.HeartbeatInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                if (!await TryRegisterAsync())
                {
                    _isRegistered = false;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _isRegistered = false;
                _logger.LogError(ex,
                    "服务 {ServiceName} 心跳注册失败，地址 {Address}",
                    _options.ServiceName, _address);
            }
        }
    }

    private async Task<bool> TryRegisterAsync()
    {
        try
        {
            var instance = BuildServiceInstance();
            _address = instance.Address;
            await _serviceRegistry.RegisterAsync(instance);
            _isRegistered = true;
            return true;
        }
        catch (ServiceRegistryException ex)
        {
            _lastRegistrationError = ex.Message;
            _logger.LogWarning("{Message}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _lastRegistrationError = ex.Message;
            _logger.LogWarning(ex, "服务 {ServiceName} 注册失败，地址 {Address}", _options.ServiceName, _address);
            return false;
        }
    }

    private ServiceInstance BuildServiceInstance()
    {
        return new ServiceInstance
        {
            ServiceName = _options.ServiceName,
            Address = ResolveAddress(),
            Metadata = _options.Metadata
        };
    }

    private string ResolveAddress()
    {
        if (!string.IsNullOrWhiteSpace(_options.Address))
        {
            return ServiceAddress.Canonicalize(_options.Address);
        }

        var urls = _configuration["ASPNETCORE_URLS"] ?? _configuration["urls"];
        if (!string.IsNullOrWhiteSpace(urls))
        {
            ServiceRegistryException? last = null;
            foreach (var candidate in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try
                {
                    return ServiceAddress.Canonicalize(candidate);
                }
                catch (ServiceRegistryException ex)
                {
                    last = ex;
                }
            }

            throw last ?? new ServiceRegistryException("服务注册地址为空，请配置 ServiceDiscovery:Address");
        }

        var host = _options.Host ?? Environment.MachineName;
        var port = _options.Port ?? (_options.IsHttps ? 443 : 80);
        var scheme = _options.IsHttps ? "https" : "http";
        return ServiceAddress.Canonicalize($"{scheme}://{host}:{port}");
    }
}


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
    private string? _instanceId;
    private string? _address;
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

        _instanceId = _options.InstanceId ?? GenerateInstanceId();
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableAutoRegistration || string.IsNullOrEmpty(_instanceId))
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
                    throw new InvalidOperationException($"Failed to register service after {retryCount} attempts");
                }

                await Task.Delay(_options.RegistrationRetryInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Service registration failed: {ServiceName}/{InstanceId}",
                    _options.ServiceName, _instanceId);

                if (_options.BlockStartupOnRegistrationFailure)
                {
                    throw;
                }

                await Task.Delay(_options.RegistrationRetryInterval, stoppingToken);
            }
        }

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
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
        catch (Exception)
        {
            return false;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableAutoRegistration || string.IsNullOrEmpty(_instanceId) || string.IsNullOrEmpty(_address))
        {
            await base.StopAsync(cancellationToken);
            return;
        }

        if (_isRegistered)
        {
            try
            {
                await _serviceRegistry.DeregisterAsync(_options.ServiceName, _address);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deregister service: {ServiceName}", _options.ServiceName);
            }
        }

        await base.StopAsync(cancellationToken);
    }
    
    private ServiceInstance BuildServiceInstance()
    {
        string address;

        if (!string.IsNullOrEmpty(_options.Address))
        {
            address = _options.Address;
        }
        else
        {
            var urls = _configuration["ASPNETCORE_URLS"] ?? _configuration["urls"];
            if (!string.IsNullOrEmpty(urls))
            {
                address = urls.Split(';').First();
            }
            else
            {
                var host = _options.Host ?? Environment.MachineName;
                var port = _options.Port ?? 80;
                address = $"{(_options.IsHttps ? "https" : "http")}://{host}:{port}";
            }
        }

        return new ServiceInstance
        {
            ServiceName = _options.ServiceName,
            InstanceId = _instanceId!,
            Address = address,
            Metadata = _options.Metadata
        };
    }
    
    private string GenerateInstanceId()
    {
        var machineName = Environment.MachineName.ToLowerInvariant();
        var timestamp = DateTime.UtcNow.Ticks;
        var random = Guid.NewGuid().ToString("N")[..8];
        
        return $"{_options.ServiceName}-{machineName}-{timestamp}-{random}";
    }
}


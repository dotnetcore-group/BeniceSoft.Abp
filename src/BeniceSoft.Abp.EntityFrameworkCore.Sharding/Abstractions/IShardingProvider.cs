using Microsoft.Extensions.DependencyInjection;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IShardingProvider : IDisposable
{
    IServiceProvider ApplicationServices { get; }

    object? GetService(Type serviceType, bool tryApplication = true);

    T? GetService<T>(bool tryApplication = true);

    object GetRequiredService(Type serviceType, bool tryApplication = true);

    T GetRequiredService<T>(bool tryApplication = true);

    IShardingProvider CreateScope();
}

internal sealed class ShardingProvider : IShardingProvider
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly IServiceScope? _scope;
    private readonly IServiceScope? _appScope;

    public ShardingProvider(IServiceProvider? serviceProvider, IServiceProvider? applicationServices)
    {
        if (serviceProvider == null && applicationServices == null)
        {
            throw new ShardingInvalidOperationException("All ServiceProvider are empty");
        }

        _serviceProvider = serviceProvider;
        ApplicationServices = applicationServices!;
    }

    private ShardingProvider(IServiceScope? scope, IServiceScope? appScope) : this(scope?.ServiceProvider, appScope?.ServiceProvider)
    {
        _scope = scope;
        _appScope = appScope;
    }

    public IServiceProvider ApplicationServices { get; }

    public object? GetService(Type serviceType, bool tryApplication = true)
    {
        var service = _serviceProvider?.GetService(serviceType);
        if (service == null)
        {
            return ApplicationServices.GetService(serviceType);
        }

        return service;
    }

    public T? GetService<T>(bool tryApplication = true)
    {
        var service = GetService(typeof(T), tryApplication);
        return service is null ? default : (T)service;
    }

    public object GetRequiredService(Type serviceType, bool tryApplication = true)
    {
        var service = GetService(serviceType, tryApplication);
        if (service == null)
        {
            throw new ArgumentNullException($"cant unable resolve service:[{serviceType}]");
        }

        return service;
    }

    public T GetRequiredService<T>(bool tryApplication = true)
    {
        return (T)GetRequiredService(typeof(T), tryApplication);
    }

    public IShardingProvider CreateScope()
    {
        return new ShardingProvider(_serviceProvider?.CreateScope(), ApplicationServices?.CreateScope());
    }

    public void Dispose()
    {
        _scope?.Dispose();
        _appScope?.Dispose();
        GC.SuppressFinalize(this);
    }
}

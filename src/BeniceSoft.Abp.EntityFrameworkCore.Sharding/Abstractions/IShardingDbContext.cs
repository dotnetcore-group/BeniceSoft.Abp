using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IShardingDbContext
{
    /// <summary>
    /// 壳 DbContext 返回执行器；物理分片 DbContext 返回 null。
    /// </summary>
    IShardingDbContextExecutor? TryGetExecutor();

    /// <summary>
    /// 同 <see cref="TryGetExecutor"/>；在物理 DbContext 上调用时抛出异常。
    /// </summary>
    IShardingDbContextExecutor GetExecutor();

    /// <summary>
    /// true 表示当前实例是物理分片 DbContext（无 Executor）；壳为 false。
    /// </summary>
    bool IsExecutor { get; }
}

public sealed class ShardingRuntimeBuilder<T>(Action<DbContextOptionsBuilder>? buildFactory = null)
    where T : DbContext, IShardingDbContext
{
    private readonly List<Action<IServiceCollection>> _serviceFactory = [];
    private Action<IShardingProvider, IShardingRouteOptions>? _routeOptionsFactory;
    private Action<IShardingProvider, ShardingOptions>? _optionsFactory;

    public ShardingRuntimeBuilder<T> UseRouteOptions(Action<IShardingProvider, IShardingRouteOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _routeOptionsFactory = configure;
        return this;
    }

    public ShardingRuntimeBuilder<T> UseOptions(Action<IShardingProvider, ShardingOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _optionsFactory = configure;
        return this;
    }

    public ShardingRuntimeBuilder<T> ConfigureServices(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _serviceFactory.Add(configure);
        return this;
    }

    internal IShardingRuntimeContext<T> Build(IServiceProvider? applicationServices = null)
    {
        var ctx = new ShardingRuntimeContext<T>();
        ctx.ConfigureServices(services =>
        {
            services.AddSingleton<IShardingRouteOptions>(sp =>
            {
                var shardingProvider = sp.GetRequiredService<IShardingProvider>();
                var routeOptions = new ShardingRouteOptions();
                _routeOptionsFactory?.Invoke(shardingProvider, routeOptions);
                return routeOptions;
            });

            services.AddSingleton(sp =>
            {
                var shardingProvider = sp.GetRequiredService<IShardingProvider>();
                var options = new ShardingOptions(buildFactory);
                _optionsFactory?.Invoke(shardingProvider, options);
                options.CheckLegality();
                return options;
            });

            services.AddLogging();
            services.AddSingleton<IShardingProvider>(sp => new ShardingProvider(sp, applicationServices));
            services.AddShardingCore<T>();
            foreach (var serviceAction in _serviceFactory)
            {
                serviceAction.Invoke(services);
            }
        });

        ctx.Initialize();
        return ctx;
    }
}

public interface IShardingTableDbContext
{
    /// <summary>
    /// 由分片引擎赋值，业务代码无须赋值
    /// </summary>
    IRouteTail RouteTail { get; set; }
}

public interface IShardingTransaction
{
    void Notify();

    void Commit();

    Task CommitAsync(CancellationToken cancellationToken = default);

    void Rollback();

    Task RollbackAsync(CancellationToken cancellationToken = default);

    void CreateSavepoint(string name);

    Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default);

    void RollbackToSavepoint(string name);

    Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default);

    void ReleaseSavepoint(string name);

    Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default);
}

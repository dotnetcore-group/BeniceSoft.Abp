using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IShardingRuntimeContext
{
    ShardingOptions Options { get; }

    IShardingProvider ShardingProvider { get; }

    Type DbContextType { get; }

    ICacheLockProvider CacheLockProvider { get; }

    IDbContextAware DbContextAware { get; }

    IDbContextOptionsBuilderCreator DbContextOptionsBuilderCreator { get; }

    IShardingRouteOptions RouteOptions { get; }

    IShardingMigrationManager MigrationManager { get; }

    IShardingComparer Comparer { get; }

    IShardingCompilerExecutor CompilerExecutor { get; }

    ISeparationManager SeparationManager { get; }

    IShardingRouteManager RouteManager { get; }

    ITrackerManager TrackerManager { get; }

    IParallelTableManager TableManager { get; }

    IDbContextCreator DbContextCreator { get; }

    IRouteTailDbContextCreator RouteTailDbContextCreator { get; }

    IEntityMetadataManager EntityMetadataManager { get; }

    IVirtualDataSource VirtualDataSource { get; }

    IDataSourceRouteManager DataSourceRouteManager { get; }

    ITableRouteManager TableRouteManager { get; }

    ITableCreator TableCreator { get; }

    IRouteTailFactory RouteTailFactory { get; }

    ISeparationConnectionFactory SeparationConnectionFactory { get; }

    IQueryTracker QueryTracker { get; }

    IMergeManager MergeManager { get; }

    IShardingPageManager PageManager { get; }

    IDynamicDataSource DynamicDataSource { get; }

    void Initialize();

    object? GetService(Type serviceType);

    TService? GetService<TService>();

    object GetRequiredService(Type serviceType);

    TService GetRequiredService<TService>() where TService : notnull;

    void GetOrCreate(DbContext ctx);
}

public interface IShardingRuntimeContext<T> : IShardingRuntimeContext
    where T : IShardingDbContext
{

}

public sealed class ShardingRuntimeContext<T> : IShardingRuntimeContext<T>
    where T : IShardingDbContext
{
    private readonly object _locker = new();
    private readonly object _lockModeled = new();
    private readonly IServiceCollection _services = new ServiceCollection();
    private bool _init;
    private bool _initModeled;
    private ServiceProvider _serviceProvider = null!;

    public Type DbContextType => typeof(T);

    public void ConfigureServices(Action<IServiceCollection> configure)
    {
        CheckIfBuild();
        configure(_services);
    }

    public void Initialize()
    {
        if (_init)
        {
            return;
        }

        lock (_locker)
        {
            if (_init)
            {
                return;
            }

            _init = true;
            _serviceProvider = _services.BuildServiceProvider();
            _serviceProvider.GetRequiredService<IShardingSeed>().Initialize();
            GetRequiredService<IShardingBootstrapper>().Create();
        }
    }

    public IDbContextAware DbContextAware => field ??= GetRequiredService<IDbContextAware>();

    public ICacheLockProvider CacheLockProvider => field ??= GetRequiredService<ICacheLockProvider>();

    public IShardingProvider ShardingProvider => field ??= GetRequiredService<IShardingProvider>();

    public IDbContextOptionsBuilderCreator DbContextOptionsBuilderCreator => field ??= GetRequiredService<IDbContextOptionsBuilderCreator>();

    public ShardingOptions Options => field ??= GetRequiredService<ShardingOptions>();

    public IShardingRouteOptions RouteOptions => field ??= GetRequiredService<IShardingRouteOptions>();

    public IShardingMigrationManager MigrationManager => field ??= GetRequiredService<IShardingMigrationManager>();

    public IShardingComparer Comparer => field ??= GetRequiredService<IShardingComparer>();

    public IShardingCompilerExecutor CompilerExecutor => field ??= GetRequiredService<IShardingCompilerExecutor>();

    public ISeparationManager SeparationManager => field ??= GetRequiredService<ISeparationManager>();

    public IShardingRouteManager RouteManager => field ??= GetRequiredService<IShardingRouteManager>();

    public ITrackerManager TrackerManager => field ??= GetRequiredService<ITrackerManager>();

    public IParallelTableManager TableManager => field ??= GetRequiredService<IParallelTableManager>();

    public IDbContextCreator DbContextCreator => field ??= GetRequiredService<IDbContextCreator>();

    public IRouteTailDbContextCreator RouteTailDbContextCreator => field ??= GetRequiredService<IRouteTailDbContextCreator>();

    public IEntityMetadataManager EntityMetadataManager => field ??= GetRequiredService<IEntityMetadataManager>();

    public IVirtualDataSource VirtualDataSource => field ??= GetRequiredService<IVirtualDataSource>();

    public IDataSourceRouteManager DataSourceRouteManager => field ??= GetRequiredService<IDataSourceRouteManager>();

    public ITableRouteManager TableRouteManager => field ??= GetRequiredService<ITableRouteManager>();

    public ISeparationConnectionFactory SeparationConnectionFactory => field ??= GetRequiredService<ISeparationConnectionFactory>();

    public ITableCreator TableCreator => field ??= GetRequiredService<ITableCreator>();

    public IRouteTailFactory RouteTailFactory => field ??= GetRequiredService<IRouteTailFactory>();

    public IQueryTracker QueryTracker => field ??= GetRequiredService<IQueryTracker>();

    public IMergeManager MergeManager => field ??= GetRequiredService<IMergeManager>();

    public IShardingPageManager PageManager => field ??= GetRequiredService<IShardingPageManager>();

    public IDynamicDataSource DynamicDataSource => field ??= GetRequiredService<IDynamicDataSource>();

    public void GetOrCreate(DbContext ctx)
    {
        if (_initModeled)
        {
            return;
        }

        lock (_lockModeled)
        {
            if (_initModeled)
            {
                return;
            }

            _initModeled = true;
            var entityMetadataManager = GetService<IEntityMetadataManager>()
                ?? throw new InvalidOperationException("Unable to resolve IEntityMetadataManager.");
            var trackerManager = GetService<ITrackerManager>()
                ?? throw new InvalidOperationException("Unable to resolve ITrackerManager.");
            var entityTypes = ctx.Model.GetEntityTypes();
            foreach (var entityType in entityTypes)
            {
                trackerManager.Add(entityType.ClrType, entityType.FindPrimaryKey() != null);
                var isOwned = entityType.IsOwned();
                if (!isOwned)
                {
                    if (!entityMetadataManager.IsSharding(entityType.ClrType))
                    {
                        var entityMetadata = new EntityMetadata(entityType.ClrType);
                        entityMetadataManager.Add(entityMetadata);
                    }

                    entityMetadataManager.Initialize(entityType);
                }
            }
        }
    }

    private void CheckIfBuild()
    {
        if (_init)
        {
            throw new InvalidOperationException("sharding runtime already build");
        }
    }

    private void CheckIfNotBuild()
    {
        if (!_init)
        {
            throw new InvalidOperationException("sharding runtime not init");
        }
    }

    public object? GetService(Type serviceType)
    {
        CheckIfNotBuild();
        return _serviceProvider.GetService(serviceType);
    }

    public TService? GetService<TService>()
    {
        CheckIfNotBuild();
        return _serviceProvider.GetService<TService>();
    }

    public object GetRequiredService(Type serviceType)
    {
        CheckIfNotBuild();
        return _serviceProvider.GetRequiredService(serviceType);
    }

    public TService GetRequiredService<TService>() where TService : notnull
    {
        CheckIfNotBuild();
        return _serviceProvider.GetRequiredService<TService>();
    }
}

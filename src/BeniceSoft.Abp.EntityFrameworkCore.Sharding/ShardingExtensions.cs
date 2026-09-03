using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// doc to https://github.com/dotnetcore/sharding-core
/// </summary>
public static class ShardingExtensions
{
    public static ShardingRuntimeBuilder<T> AddShardingDb<T>(this IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> optionsAction, Action<DbContextOptionsBuilder>? buildFactory = null, ServiceLifetime contextLifetime = ServiceLifetime.Scoped, ServiceLifetime optionsLifetime = ServiceLifetime.Scoped)
        where T : DbContext, IShardingDbContext
    {
        if (contextLifetime == ServiceLifetime.Singleton)
        {
            throw new NotSupportedException($"{nameof(contextLifetime)}:{nameof(ServiceLifetime.Singleton)}");
        }

        if (optionsLifetime == ServiceLifetime.Singleton)
        {
            throw new NotSupportedException($"{nameof(optionsLifetime)}:{nameof(ServiceLifetime.Singleton)}");
        }

        var builder = services.AddSharding<T>(buildFactory);
        services.AddDbContext<T>((s, b) =>
        {
            optionsAction(s, b);
            buildFactory?.Invoke(b);
            b.UseSharding<T>(s);
        }, contextLifetime, optionsLifetime);
        return builder;
    }

    public static ShardingRuntimeBuilder<T> AddSharding<T>(this IServiceCollection services, Action<DbContextOptionsBuilder>? buildFactory = null)
        where T : DbContext, IShardingDbContext
    {
        var builder = new ShardingRuntimeBuilder<T>(buildFactory);
        services.TryAddSingleton<IShardingRuntimeContext<T>>(builder.Build);
        services.TryAddSingleton<IShardingRuntimeContext>(s => s.GetService<IShardingRuntimeContext<T>>()!);
        return builder;
    }

    private static DbContextOptionsBuilder UseSharding<T>(this DbContextOptionsBuilder optionsBuilder, IShardingRuntimeContext context)
        where T : DbContext, IShardingDbContext
    {
        var options = context.Options;
        options.MigrationFactory?.Invoke(optionsBuilder);
        var virtualDataSource = context.VirtualDataSource;
        var connectionString = virtualDataSource.GetConnectionString(virtualDataSource.DefaultDataSource);
        virtualDataSource.Options.UseDbContextOptionsBuilder(connectionString, optionsBuilder).UseSharding(context);
        virtualDataSource.Options.UseShellDbContextOptionBuilder(optionsBuilder);
        return optionsBuilder;
    }

    public static void UseSharding<T>(this DbContextOptionsBuilder optionsBuilder, IServiceProvider serviceProvider)
        where T : DbContext, IShardingDbContext
    {
        var context = serviceProvider.GetRequiredService<IShardingRuntimeContext<T>>();
        optionsBuilder.UseSharding<T>(context);
    }

    /// <summary>
    /// 先执行 Provider / Hint 等配置，再挂 <see cref="UseSharding{T}(DbContextOptionsBuilder, IServiceProvider)"/>。
    /// 任何二次 <c>AbpDbContextOptions.Configure</c> 覆盖连接时也应走此入口，避免冲掉分片。
    /// </summary>
    public static DbContextOptionsBuilder UseShardingAfter<T>(
        this DbContextOptionsBuilder optionsBuilder,
        IServiceProvider serviceProvider,
        Action<DbContextOptionsBuilder>? configureBeforeSharding = null)
        where T : DbContext, IShardingDbContext
    {
        configureBeforeSharding?.Invoke(optionsBuilder);
        optionsBuilder.UseSharding<T>(serviceProvider);
        return optionsBuilder;
    }

    private static void UseSharding(this DbContextOptionsBuilder optionsBuilder, IShardingRuntimeContext context)
    {
        optionsBuilder.UseShardingWrapMark();
        optionsBuilder.ReplaceService<IMigrator, ShardingMigrator>();
        optionsBuilder.UseShardingOptions(context);
        optionsBuilder.ReplaceService<IQueryCompiler, ShardingQueryCompiler>();
        optionsBuilder.ReplaceService<IDbSetInitializer, ShardingDbSetInitializer>();
        optionsBuilder.ReplaceService<IDbSetSource, ShardingDbSetSource>();
        optionsBuilder.ReplaceService<IChangeTrackerFactory, ShardingChangeTrackerFactory>();
        optionsBuilder.ReplaceService<IDbContextTransactionManager, ShardingRelationalTransactionManager>();
        optionsBuilder.ReplaceService<IStateManager, ShardingStateManager>();
        optionsBuilder.ReplaceService<IRelationalTransactionFactory, ShardingRelationalTransactionFactory>();
    }

    internal static IServiceCollection AddShardingCore<T>(this IServiceCollection services)
        where T : DbContext, IShardingDbContext
    {
        services.TryAddSingleton<IDbContextAware>(sp => new DbContextAware(typeof(T)));

        services.TryAddSingleton<IShardingSeed, ShardingSeed>();
        services.TryAddSingleton<IShardingBootstrapper, ShardingBootstrapper>();

        services.TryAddSingleton<ICacheLockProvider, CacheLockProvider>();
        services.TryAddSingleton<IDynamicDataSource, DynamicDataSource>();
        services.TryAddSingleton<ITableRouteManager, TableRouteManager>();
        services.TryAddSingleton<IVirtualDataSourceOptions, VirtualDataSourceOptions>();

        //分表dbcontext创建
        services.TryAddSingleton<IDbContextCreator, DbContextCreator<T>>();
        services.TryAddSingleton<IRouteTailDbContextCreator, RouteTailDbContextCreator>();
        services.TryAddSingleton<IDbContextOptionsBuilderCreator, DbContextOptionsBuilderCreator>();

        services.TryAddSingleton<ITrackerManager, TrackerManager>();
        services.TryAddSingleton<IStreamMergeContextFactory, StreamMergeContextFactory>();
        services.TryAddSingleton<ITableCreator, TableCreator>();
        //虚拟数据源管理
        services.TryAddSingleton<IVirtualDataSource, VirtualDataSource>();
        services.TryAddSingleton<IDataSourceRouteManager, DataSourceRouteManager>();
        services.TryAddSingleton<IDataSourceRouteRule, DataSourceRouteRule>();
        services.TryAddSingleton<IDataSourceRouteRuleFactory, DataSourceRouteRuleFactory>();

        //读写分离链接创建工厂
        services.TryAddSingleton<ISeparationAccessor, SeparationAccessor>();
        services.TryAddSingleton<ISeparationConnectionFactory, SeparationConnectionFactory>();

        //分表分库对象元信息管理
        services.TryAddSingleton<IEntityMetadataManager, EntityMetadataManager>();

        //分表引擎
        services.TryAddSingleton<ITableRouteRuleFactory, TableRouteRuleFactory>();
        services.TryAddSingleton<ITableRouteRule, TableRouteRule>();
        //分表引擎工程
        services.TryAddSingleton<IParallelTableManager, ParallelTableManager>();
        services.TryAddSingleton<IRouteTailFactory, RouteTailFactory>();
        services.TryAddSingleton<IShardingCompilerExecutor, ShardingCompilerExecutor>();
        services.TryAddSingleton<IQueryCompilerContextFactory, QueryCompilerContextFactory>();
        services.TryAddSingleton<IShardingQueryExecutor, ShardingQueryExecutor>();

        services.TryAddSingleton<IPrepareParser, PrepareParser>();
        services.TryAddSingleton<IQueryableParse, QueryableParse>();
        services.TryAddSingleton<IQueryableRewrite, QueryableRewrite>();
        services.TryAddSingleton<IQueryableOptimize, QueryableOptimize>();

        //migration manage
        services.TryAddSingleton<IShardingMigrationAccessor, ShardingMigrationAccessor>();
        services.TryAddSingleton<IShardingMigrationManager, ShardingMigrationManager>();

        //route manage
        services.TryAddSingleton<IShardingRouteManager, ShardingRouteManager>();
        services.TryAddSingleton<IShardingRouteAccessor, ShardingRouteAccessor>();

        //sharding page
        services.TryAddSingleton<IShardingPageManager, ShardingPageManager>();
        services.TryAddSingleton<IShardingPageAccessor, ShardingPageAccessor>();

        services.TryAddSingleton<IShardingBootstrapper, ShardingBootstrapper>();
        services.TryAddSingleton<IMergeManager, MergeManager>();
        services.TryAddSingleton<IMergeAccessor, MergeAccessor>();
        services.TryAddSingleton<IQueryTracker, QueryTracker>();
        services.TryAddSingleton<IShardingTrackingExecutor, ShardingTrackingExecutor>();
        services.TryAddSingleton<INativeTrackingExecutor, NativeTrackingExecutor>();

        //读写分离手动指定
        services.TryAddSingleton<ISeparationManager, SeparationManager>();
        services.TryAddSingleton<IShardingComparer, ShardingComparer>();
        services.TryAddSingleton<ITableEnsureManager, GuessTableEnsureManager>();

        services.TryAddSingleton<JobRunnerService>();
        services.TryAddSingleton<IShardingJobManager, ShardingJobManager>();
        return services;
    }

    internal static DbContextOptionsBuilder UseShardingOptions(this DbContextOptionsBuilder optionsBuilder, IShardingRuntimeContext context)
    {

        var extension = optionsBuilder.Options.FindExtension<ShardingOptionsExtension>() ?? new ShardingOptionsExtension(context);

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        return optionsBuilder;
    }

    private static DbContextOptionsBuilder UseShardingWrapMark(this DbContextOptionsBuilder optionsBuilder)
    {
        var extension = optionsBuilder.Options.FindExtension<ShardingWrapOptionsExtension>() ?? new ShardingWrapOptionsExtension();

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        return optionsBuilder;
    }

    internal static DbContextOptionsBuilder UseShardingInnerDb(this DbContextOptionsBuilder optionsBuilder)
    {
        return optionsBuilder.ReplaceService<IModelCacheKeyFactory, ShardingModelCacheKeyFactory>().ReplaceService<IModelSource, ShardingModelSource>().ReplaceService<IModelCustomizer, ShardingModelCustomizer>();
    }

    /// <summary>
    /// 自动尝试补偿表
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="parallelCount"></param>
    public static void UseCompensate(this IServiceProvider serviceProvider, int? parallelCount = null)
    {
        var context = serviceProvider.GetRequiredService<IShardingRuntimeContext>();
        var virtualDataSource = context.VirtualDataSource;
        var dataSource = context.DynamicDataSource;
        var options = context.Options;
        var count = parallelCount ?? options.CompensateTableParallelCount;
        if (count <= 0)
        {
            throw new ShardingInvalidOperationException($"compensate table parallel count must > 0");
        }

        var allDataSource = virtualDataSource.GetAllDataSource();
        var units = allDataSource.Partition(count);
        foreach (var migrationUnits in units)
        {
            var tasks = migrationUnits.Select(o => Task.Run(() => dataSource.Initialize(o, true, true))).ToArray();
            Task.WaitAll(tasks);
        }
    }
}

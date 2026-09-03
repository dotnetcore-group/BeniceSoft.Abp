using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IShardingDbContextExecutor : IShardingTransaction, IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 使用对象创建DbContext的前执行
    /// </summary>
    event EventHandler<EntityCreatingDbContextEventArgs> EntityCreatingDbContext;

    /// <summary>
    /// 使用对象创建DbContext的后执行
    /// </summary>
    event EventHandler<EntityCreatedDbContextEventArgs> EntityCreatedDbContext;

    /// <summary>
    /// 使用tail创建DbContext的前执行
    /// </summary>
    event EventHandler<CreatingDbContextEventArgs> CreatingDbContext;

    /// <summary>
    /// 使用tail创建DbContext的后执行
    /// </summary>
    event EventHandler<CreatedDbContextEventArgs> CreatedDbContext;

    int SeparationPriority { get; set; }

    SeparationBehavior SeparationBehavior { get; set; }

    /// <summary>
    /// has multi db context
    /// </summary>
    bool MultipleDb { get; }

    /// <summary>
    /// create sharding db context options
    /// </summary>
    /// <param name="strategy">如果当前查询需要多链接的情况下那么将使用<code>IndependentConnectionQuery</code>否则使用<code>ShareConnection</code></param>
    /// <param name="dataSource">data source name</param>
    /// <param name="routeTail"></param>
    /// <returns></returns>
    DbContext Create(CreateDbStrategy strategy, string dataSource, IRouteTail routeTail);

    DbContext Create<T>(T entity)
        where T : class;

    IVirtualDataSource GetVirtualDataSource();

    int SaveChanges(bool acceptAllChangesOnSuccess = true);

    Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess = true, CancellationToken cancellationToken = default);

    DbContext GetShellDbContext();

    IDictionary<string, IDataSourceDbContext> GetAll();
}

internal sealed class ShardingDbContextExecutor : IShardingDbContextExecutor
{
    private readonly ConcurrentDictionary<string, IDataSourceDbContext> _caches = new();
    private readonly DbContext _db;
    private readonly IShardingRuntimeContext _ctx;
    private readonly IVirtualDataSource _virtualDataSource;
    private readonly IRouteTailDbContextCreator _creator;
    private readonly ActualConnectionStringManager _manager;
    private readonly ITrackerManager _trackerManager;
    private readonly IDataSourceRouteManager _dataSourceManager;
    private readonly IEntityMetadataManager _metadataManager;
    private readonly ITableRouteManager _tableManager;
    private readonly IRouteTailFactory _tailFactory;
    private readonly ShardingOptions _options;
    private readonly ILogger<ShardingDbContextExecutor> _logger;

    public ShardingDbContextExecutor(DbContext db)
    {
        _ctx = db.GetRuntimeContext();
        _db = db;
        _virtualDataSource = _ctx.VirtualDataSource;
        _creator = _ctx.RouteTailDbContextCreator;
        _manager = new(_ctx.SeparationManager, _virtualDataSource, _db);
        _trackerManager = _ctx.TrackerManager;
        _dataSourceManager = _ctx.DataSourceRouteManager;
        _metadataManager = _ctx.EntityMetadataManager;
        _tableManager = _ctx.TableRouteManager;
        _tailFactory = _ctx.RouteTailFactory;
        _options = _ctx.Options;
        _logger = _ctx.GetRequiredService<ILoggerFactory>().CreateLogger<ShardingDbContextExecutor>();
    }

    public bool MultipleDb => _caches.Count > 1 || _caches.Values.Sum(t => t.Count) > 1;

    public int SeparationPriority
    {
        get => _manager.Priority;
        set => _manager.Priority = value;
    }

    public SeparationBehavior SeparationBehavior
    {
        get => _manager.Behavior;
        set => _manager.Behavior = value;
    }

    private event EventHandler<EntityCreatingDbContextEventArgs>? _entityCreatingDbContext;
    public event EventHandler<EntityCreatingDbContextEventArgs> EntityCreatingDbContext
    {
        add { _entityCreatingDbContext += value; }
        remove { _entityCreatingDbContext -= value; }
    }

    private event EventHandler<EntityCreatedDbContextEventArgs>? _entityCreatedDbContext;
    public event EventHandler<EntityCreatedDbContextEventArgs> EntityCreatedDbContext
    {
        add { _entityCreatedDbContext += value; }
        remove { _entityCreatedDbContext -= value; }
    }

    private event EventHandler<CreatingDbContextEventArgs>? _creatingDbContext;
    public event EventHandler<CreatingDbContextEventArgs> CreatingDbContext
    {
        add { _creatingDbContext += value; }
        remove { _creatingDbContext -= value; }
    }

    private event EventHandler<CreatedDbContextEventArgs>? _createdDbContext;
    public event EventHandler<CreatedDbContextEventArgs> CreatedDbContext
    {
        add { _createdDbContext += value; }
        remove { _createdDbContext -= value; }
    }

    public void Commit()
    {
        foreach (var cache in _caches)
        {
            try
            {
                cache.Value.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ShardingDbContextExecutor Commit");
                throw;
            }
        }

        UseWriteConnection();
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        foreach (var cache in _caches)
        {
            try
            {
                await cache.Value.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ShardingDbContextExecutor CommitAsync");
                throw;
            }
        }

        UseWriteConnection();
    }

    public DbContext Create(CreateDbStrategy strategy, string dataSource, IRouteTail routeTail)
    {
        _creatingDbContext?.Invoke(this, new CreatingDbContextEventArgs(strategy, dataSource, routeTail));

        DbContext db;
        if (strategy == CreateDbStrategy.Share)
        {
            var ctx = _caches.GetOrAdd(dataSource, ds => new DataSourceDbContext(ds, _virtualDataSource.IsDefault(ds), _db, _creator, _manager));
            db = ctx.Create(routeTail);
        }
        else
        {
            var options = CreateOptions(dataSource, strategy);
            db = _creator.Create(_db, new ShardingDbContextOptions(options, routeTail));
            db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        _createdDbContext?.Invoke(this, new CreatedDbContextEventArgs(strategy, dataSource, routeTail, db));

        return db;
    }

    private DbContextOptions CreateOptions(string dataSource, CreateDbStrategy strategy)
    {
        var dbContextOptionBuilder = _ctx.DbContextOptionsBuilderCreator.Create(_db);
        var connectionString = _manager.GetConnectionString(dataSource,
           strategy == CreateDbStrategy.ParallelWrite);
        _virtualDataSource.UseDbContextOptionsBuilder(connectionString, dbContextOptionBuilder).UseShardingOptions(_ctx);
        return dbContextOptionBuilder.Options;
    }

    private string GetTableTail(string dataSource, object entity, Type type)
    {
        if (!_metadataManager.IsShardingTable(type))
        {
            return string.Empty;
        }

        return _tableManager.RouteTo(dataSource, type, new ShardingTableRoute(null, entity))[0].Tail;
    }

    public DbContext Create<T>(T entity) where T : class
    {
        _entityCreatingDbContext?.Invoke(this, new EntityCreatingDbContextEventArgs(entity));

        var type = _trackerManager.Translate(entity.GetType());
        var dataSource = _dataSourceManager.RouteTo(type, new ShardingDataSourceRoute(null, entity))[0];
        var tail = GetTableTail(dataSource, entity, type);

        var db = Create(CreateDbStrategy.Share, dataSource, _tailFactory.Create(tail));

        _entityCreatedDbContext?.Invoke(this, new EntityCreatedDbContextEventArgs(entity, db));

        return db;
    }

    private void UseWriteConnection()
    {
        if (!_options.AutoUseWriteDb)
        {
            return;
        }

        if (_virtualDataSource.ConnectionManager is not SeparationConnectionManager)
        {
            return;
        }

        var shardingDb = _db as IShardingDbContext;
        var executor = shardingDb!.GetExecutor();
        var separationManager = _ctx.GetService<ISeparationManager>()
            ?? throw new InvalidOperationException("Unable to resolve ISeparationManager.");
        var context = separationManager.Current;
        if (context != null)
        {
            if (context.Priority > executor.SeparationPriority)
            {
                executor.SeparationPriority = context.Priority + 1;
            }
        }

        executor.SeparationBehavior = SeparationBehavior.Disable;
    }

    public void CreateSavepoint(string name)
    {
        foreach (var cache in _caches)
        {
            cache.Value.CreateSavepoint(name);
        }
    }

    public async Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        foreach (var cache in _caches)
        {
            await cache.Value.CreateSavepointAsync(name, cancellationToken);
        }
    }

    public void Dispose()
    {
        foreach (var cache in _caches)
        {
            cache.Value.Dispose();
        }

        _caches.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var cache in _caches)
        {
            await cache.Value.DisposeAsync();
        }

        _caches.Clear();
    }

    public IDictionary<string, IDataSourceDbContext> GetAll()
    {
        return _caches;
    }

    public DbContext GetShellDbContext()
    {
        return _db;
    }

    public IVirtualDataSource GetVirtualDataSource()
    {
        return _virtualDataSource;
    }

    public void Notify()
    {
        foreach (var cache in _caches)
        {
            cache.Value.Notify();
        }
    }

    public void ReleaseSavepoint(string name)
    {
        foreach (var cache in _caches)
        {
            cache.Value.ReleaseSavepoint(name);
        }
    }

    public async Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        foreach (var cache in _caches)
        {
            await cache.Value.ReleaseSavepointAsync(name, cancellationToken);
        }
    }

    public void Rollback()
    {
        foreach (var cache in _caches)
        {
            cache.Value.Rollback();
        }

        UseWriteConnection();
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        foreach (var cache in _caches)
        {
            await cache.Value.RollbackAsync(cancellationToken);
        }

        UseWriteConnection();
    }

    public void RollbackToSavepoint(string name)
    {
        foreach (var cache in _caches)
        {
            cache.Value.RollbackToSavepoint(name);
        }
    }

    public async Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        foreach (var cache in _caches)
        {
            await cache.Value.RollbackToSavepointAsync(name, cancellationToken);
        }
    }

    public int SaveChanges(bool acceptAllChangesOnSuccess = true)
    {
        EnsureShardingKeysUnchanged();
        var i = 0;
        foreach (var cache in _caches)
        {
            i += cache.Value.SaveChanges(acceptAllChangesOnSuccess);
        }

        UseWriteConnection();
        return i;
    }

    public async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess = true, CancellationToken cancellationToken = default)
    {
        EnsureShardingKeysUnchanged();
        var i = 0;
        foreach (var cache in _caches)
        {
            i += await cache.Value.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        UseWriteConnection();
        return i;
    }

    /// <summary>
    /// 已追踪实体不允许修改分片键（分表/分库属性），否则会静默写回原物理表导致数据错位。
    /// </summary>
    private void EnsureShardingKeysUnchanged()
    {
        foreach (var dataSource in _caches.Values)
        {
            foreach (var db in dataSource.GetDbContext().Values)
            {
                db.ChangeTracker.DetectChanges();
                foreach (var entry in db.ChangeTracker.Entries())
                {
                    if (entry.State != EntityState.Modified)
                    {
                        continue;
                    }

                    var clrType = entry.Metadata.ClrType;
                    var metadata = _metadataManager.TryGet(clrType);
                    if (metadata == null)
                    {
                        continue;
                    }

                    foreach (var propertyName in metadata.TableProperties.Keys.Concat(metadata.DataSourceProperties.Keys))
                    {
                        if (entry.Property(propertyName).IsModified)
                        {
                            throw new ShardingInvalidOperationException(
                                $"sharding key [{propertyName}] of [{clrType.Name}] cannot be modified after the entity is tracked. Delete and re-insert instead.");
                        }
                    }
                }
            }
        }
    }
}

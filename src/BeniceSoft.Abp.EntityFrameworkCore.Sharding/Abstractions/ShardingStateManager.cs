using BeniceSoft.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Reflection;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal sealed class ShardingStateManager : StateManager
{
    private readonly IShardingDbContext _shardingDbContext;

    public ShardingStateManager(StateManagerDependencies dependencies) : base(dependencies)
    {
        _shardingDbContext = (IShardingDbContext)Context;
    }

    public override InternalEntityEntry GetOrCreateEntry(object entity)
    {
        var ctx = _shardingDbContext.GetExecutor().Create(entity);
        var stateManager = GetStateManager(ctx);
        return stateManager.GetOrCreateEntry(entity);
    }

    public override InternalEntityEntry GetOrCreateEntry(object entity, IEntityType? entityType)
    {
        var ctx = _shardingDbContext.GetExecutor().Create(entity);
        // 尊重传入的 entityType（TPH/TPT），映射到物理 Model 上的同名/同 ClrType 实体
        var findEntityType = ResolvePhysicalEntityType(ctx, entityType, entity);
        var stateManager = GetStateManager(ctx);
        return stateManager.GetOrCreateEntry(entity, findEntityType);
    }

    public override InternalEntityEntry StartTrackingFromQuery(IEntityType baseEntityType, object entity, in ISnapshot snapshot)
    {
        var existing = TryGetEntry(entity);
        if (existing != null)
        {
            return existing;
        }

        var ctx = _shardingDbContext.GetExecutor().Create(entity);
        var physicalType = ResolvePhysicalEntityType(ctx, baseEntityType, entity);
        return GetStateManager(ctx).StartTrackingFromQuery(physicalType, entity, in snapshot);
    }

    public override InternalEntityEntry? TryGetEntry(object entity, bool throwOnNonUniqueness = true)
    {
        return FindAcrossPhysical(sm => sm.TryGetEntry(entity, throwOnNonUniqueness: false), throwOnNonUniqueness, entity);
    }

    public override InternalEntityEntry? TryGetEntry(object entity, IEntityType entityType, bool throwOnTypeMismatch = true)
    {
        return FindAcrossPhysical(sm =>
        {
            var physicalType = TryResolvePhysicalEntityType(sm.Context, entityType, entity);
            return physicalType == null ? null : sm.TryGetEntry(entity, physicalType, throwOnTypeMismatch: false);
        }, throwOnNonUniqueness: throwOnTypeMismatch, entity);
    }

    public override InternalEntityEntry? TryGetEntry(IKey key, IReadOnlyList<object?> keyValues)
    {
        return FindAcrossPhysical(sm =>
        {
            var physicalKey = ResolvePhysicalKey(sm.Context, key);
            return physicalKey == null ? null : sm.TryGetEntry(physicalKey, keyValues);
        }, throwOnNonUniqueness: false, entity: null);
    }

    public override InternalEntityEntry? TryGetEntry(IKey key, object?[] keyValues, bool throwOnNullKey, out bool hasNullKey)
    {
        hasNullKey = false;
        InternalEntityEntry? found = null;
        var anyNullKey = false;
        foreach (var sm in EnumeratePhysicalStateManagers())
        {
            var physicalKey = ResolvePhysicalKey(sm.Context, key);
            if (physicalKey == null)
            {
                continue;
            }

            var entry = sm.TryGetEntry(physicalKey, keyValues, throwOnNullKey: false, out var nullKey);
            anyNullKey |= nullKey;
            if (entry != null)
            {
                if (found != null)
                {
                    throw new InvalidOperationException(
                        $"The instance of entity type '{key.DeclaringEntityType.DisplayName()}' cannot be tracked because it is defined by multiple physical sharding DbContexts.");
                }

                found = entry;
            }
        }

        hasNullKey = found == null && anyNullKey;
        if (hasNullKey && throwOnNullKey)
        {
            throw new InvalidOperationException(
                $"The key value for entity type '{key.DeclaringEntityType.DisplayName()}' cannot be null.");
        }

        return found;
    }

    public override InternalEntityEntry? TryGetExistingEntry(object entity, IKey key)
    {
        return FindAcrossPhysical(sm =>
        {
            var physicalKey = ResolvePhysicalKey(sm.Context, key);
            return physicalKey == null ? null : sm.TryGetExistingEntry(entity, physicalKey);
        }, throwOnNonUniqueness: false, entity);
    }

    private InternalEntityEntry? FindAcrossPhysical(
        Func<IStateManager, InternalEntityEntry?> finder,
        bool throwOnNonUniqueness,
        object? entity)
    {
        var executor = _shardingDbContext.TryGetExecutor();
        if (executor == null)
        {
            return null;
        }

        InternalEntityEntry? found = null;
        foreach (var sm in EnumeratePhysicalStateManagers(executor))
        {
            var entry = finder(sm);
            if (entry == null)
            {
                continue;
            }

            if (found != null)
            {
                if (throwOnNonUniqueness)
                {
                    var typeName = entity?.GetType().ShortDisplayName() ?? found.EntityType.DisplayName();
                    throw new InvalidOperationException(
                        $"The instance of entity type '{typeName}' cannot be tracked because it is defined by multiple physical sharding DbContexts.");
                }

                return found;
            }

            found = entry;
        }

        return found;
    }

    private IEnumerable<IStateManager> EnumeratePhysicalStateManagers()
    {
        var executor = _shardingDbContext.TryGetExecutor();
        if (executor == null)
        {
            yield break;
        }

        foreach (var sm in EnumeratePhysicalStateManagers(executor))
        {
            yield return sm;
        }
    }

    private static IEnumerable<IStateManager> EnumeratePhysicalStateManagers(IShardingDbContextExecutor executor)
    {
        foreach (var dataSource in executor.GetAll().Values)
        {
            foreach (var db in dataSource.GetDbContext().Values)
            {
                yield return GetStateManager(db);
            }
        }
    }

    private static IStateManager GetStateManager(DbContext ctx)
        => ctx.GetService<IDbContextDependencies>().StateManager;

    private static IEntityType ResolvePhysicalEntityType(DbContext ctx, IEntityType? shellEntityType, object entity)
        => TryResolvePhysicalEntityType(ctx, shellEntityType, entity)
           ?? throw new ShardingInvalidOperationException(
               $"cant map entity type [{shellEntityType?.Name}] / [{entity.GetType().FullName}] to physical model of [{ctx.GetType().Name}]");

    private static IEntityType? TryResolvePhysicalEntityType(DbContext ctx, IEntityType? shellEntityType, object entity)
    {
        var model = ctx.Model;
        if (shellEntityType is null)
        {
            return model.FindEntityType(entity.GetType());
        }

        return model.FindEntityType(shellEntityType.Name)
               ?? model.FindEntityType(shellEntityType.ClrType)
               ?? model.FindEntityType(entity.GetType());
    }

    private static IKey? ResolvePhysicalKey(DbContext ctx, IKey shellKey)
    {
        var entityType = ctx.Model.FindEntityType(shellKey.DeclaringEntityType.Name)
                         ?? ctx.Model.FindEntityType(shellKey.DeclaringEntityType.ClrType);
        if (entityType == null)
        {
            return null;
        }

        var names = shellKey.Properties.Select(p => p.Name).ToArray();
        return entityType.GetKeys().FirstOrDefault(k =>
            k.Properties.Count == names.Length
            && k.Properties.Select(p => p.Name).SequenceEqual(names));
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var i = 0;
        //如果是内部开的事务就内部自己消化
        if (Context.Database.AutoTransactionBehavior != AutoTransactionBehavior.Never && Context.Database.CurrentTransaction == null && _shardingDbContext.GetExecutor().MultipleDb)
        {
            using var tran = Context.Database.BeginTransaction();
            i = _shardingDbContext.GetExecutor().SaveChanges(acceptAllChangesOnSuccess);
            tran.Commit();
        }
        else
        {
            i = _shardingDbContext.GetExecutor().SaveChanges(acceptAllChangesOnSuccess);
        }

        return i;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var i = 0;
        //如果是内部开的事务就内部自己消化
        if (Context.Database.AutoTransactionBehavior != AutoTransactionBehavior.Never && Context.Database.CurrentTransaction == null && _shardingDbContext.GetExecutor().MultipleDb)
        {
            using var tran = await Context.Database.BeginTransactionAsync(cancellationToken);
            i = await _shardingDbContext.GetExecutor().SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            await tran.CommitAsync(cancellationToken);
        }
        else
        {
            i = await _shardingDbContext.GetExecutor().SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        return i;
    }
}

internal sealed class ShardingChangeTracker(DbContext ctx, IStateManager stateManager, IChangeDetector changeDetector, IModel model, IEntityEntryGraphIterator graphIterator) : ChangeTracker(ctx, stateManager, changeDetector, model,
    graphIterator)
{
    private readonly DbContext _ctx = ctx;

    private IShardingDbContextExecutor? TryExecutor()
        => _ctx is IShardingDbContext sharding ? sharding.TryGetExecutor() : null;

    public override bool HasChanges()
    {
        var executor = TryExecutor();
        if (executor != null)
        {
            return executor.GetAll().Any(o => o.Value.GetDbContext().Any(r => r.Value.ChangeTracker.HasChanges()));
        }

        return base.HasChanges();
    }

    public override IEnumerable<EntityEntry> Entries()
    {
        var executor = TryExecutor();
        if (executor != null)
        {
            return executor.GetAll().SelectMany(o => o.Value.GetDbContext().SelectMany(cd => cd.Value.ChangeTracker.Entries()));
        }

        return base.Entries();
    }

    public override IEnumerable<EntityEntry<T>> Entries<T>()
    {
        var executor = TryExecutor();
        if (executor != null)
        {
            return executor.GetAll().SelectMany(o => o.Value.GetDbContext().SelectMany(cd => cd.Value.ChangeTracker.Entries<T>()));
        }

        return base.Entries<T>();
    }

    public override void DetectChanges()
    {
        if (TryExecutor() != null)
        {
            Do(c => c.DetectChanges());
            return;
        }

        base.DetectChanges();
    }

    public override void AcceptAllChanges()
    {
        if (TryExecutor() != null)
        {
            Do(c => c.AcceptAllChanges());
            return;
        }

        base.AcceptAllChanges();
    }

    private void Do(Action<ChangeTracker> action)
    {
        var executor = TryExecutor();
        if (executor == null)
        {
            return;
        }

        foreach (var dataSourceDbContext in executor.GetAll())
        {
            foreach (var keyValuePair in dataSourceDbContext.Value.GetDbContext())
            {
                action(keyValuePair.Value.ChangeTracker);
            }
        }
    }

    public override void TrackGraph(object rootEntity, Action<EntityEntryGraphNode> callback)
    {
        var executor = TryExecutor();
        if (executor != null)
        {
            var genericDbContext = executor.Create(rootEntity);
            genericDbContext.ChangeTracker.TrackGraph(rootEntity, callback);
            return;
        }

        base.TrackGraph(rootEntity, callback);
    }

    public override void TrackGraph<TState>(object rootEntity, TState state, Func<EntityEntryGraphNode<TState>, bool> callback) where TState : default
    {
        var executor = TryExecutor();
        if (executor != null)
        {
            var genericDbContext = executor.Create(rootEntity);
            genericDbContext.ChangeTracker.TrackGraph(rootEntity, state, callback);
            return;
        }

        base.TrackGraph(rootEntity, state, callback);
    }

    public override void CascadeChanges()
    {
        if (TryExecutor() != null)
        {
            Do(c => c.CascadeChanges());
            return;
        }

        base.CascadeChanges();
    }

    public override void Clear()
    {
        if (TryExecutor() != null)
        {
            Do(c => c.Clear());
            return;
        }

        base.Clear();
    }
}

internal sealed class ShardingChangeTrackerFactory(ICurrentDbContext currentContext, IStateManager stateManager, IChangeDetector changeDetector, IModel model, IEntityEntryGraphIterator graphIterator) : ChangeTrackerFactory(currentContext, stateManager, changeDetector, model, graphIterator)
{
    private readonly ICurrentDbContext _currentContext = currentContext;
    private readonly IStateManager _stateManager = stateManager;
    private readonly IChangeDetector _changeDetector = changeDetector;
    private readonly IModel _model = model;
    private readonly IEntityEntryGraphIterator _graphIterator = graphIterator;

    public override ChangeTracker Create()
    {
        return new ShardingChangeTracker(_currentContext.Context, _stateManager, _changeDetector, _model, _graphIterator);
    }
}

internal sealed class ShardingModelSource(ModelSourceDependencies dependencies, IShardingRuntimeContext context) : ModelSource(dependencies)
{
    private readonly IShardingRuntimeContext _context = context;

    /// <summary>
    ///     Dependencies for this service.
    /// </summary>
    protected override ModelSourceDependencies Dependencies { get; } = dependencies;

    /// <summary>
    ///     Gets the model to be used.
    /// </summary>
    /// <param name="ctx">The context the model is being produced for.</param>
    /// <param name="modelCreationDependencies">The dependencies object used during the creation of the model.</param>
    /// <param name="designTime">Whether the model should contain design-time configuration.</param>
    /// <returns>The model to be used.</returns>
    public override IModel GetModel(DbContext ctx, ModelCreationDependencies modelCreationDependencies, bool designTime)
    {
        CacheItemPriority? setPriority = null;
        if (ctx is IShardingTableDbContext shardingTableDbContext)
        {
            if (shardingTableDbContext.RouteTail is null)
            {
                throw new ShardingInvalidOperationException("db context model is inited before RouteTail set value");
            }

            if (shardingTableDbContext.RouteTail is INoCacheRouteTail)
            {
                var noCacheModel = CreateModel(ctx, modelCreationDependencies.ConventionSetBuilder, modelCreationDependencies.ModelDependencies);
                noCacheModel = modelCreationDependencies.ModelRuntimeInitializer.Initialize(noCacheModel,
                    designTime, modelCreationDependencies.ValidationLogger);
                return noCacheModel;
            }
            else if (shardingTableDbContext.RouteTail is ISingleRouteTail singleRouteTail && singleRouteTail.ShardingTable)
            {
                setPriority = CacheItemPriority.Normal;
            }
        }

        var cache = Dependencies.MemoryCache;
        var cacheKey = Dependencies.ModelCacheKeyFactory.Create(ctx, designTime);
        if (!cache.TryGetValue(cacheKey, out IModel? model))
        {
            var cacheLockProvider = _context.CacheLockProvider;

            var priority = setPriority ?? cacheLockProvider.GetPriority();
            var size = cacheLockProvider.GetEntrySize();
            var waitSeconds = cacheLockProvider.GetWaitSeconds();
            var cacheLockObject = cacheLockProvider.GetObject(cacheKey);
            // Make sure OnModelCreating really only gets called once, since it may not be thread safe.
            var acquire = Monitor.TryEnter(cacheLockObject, TimeSpan.FromSeconds(waitSeconds));
            if (!acquire)
            {
                throw new ShardingInvalidOperationException("cache model timeout");
            }

            try
            {
                if (!cache.TryGetValue(cacheKey, out model))
                {
                    model = CreateModel(ctx, modelCreationDependencies.ConventionSetBuilder, modelCreationDependencies.ModelDependencies);
                    model = modelCreationDependencies.ModelRuntimeInitializer.Initialize(model, designTime, modelCreationDependencies.ValidationLogger);
                    model = cache.Set(cacheKey, model, new MemoryCacheEntryOptions { Size = size, Priority = priority });
                }
            }
            finally
            {
                Monitor.Exit(cacheLockObject);
            }
        }

        return model ?? throw new ShardingInvalidOperationException("cant resolve model from cache");
    }
}

internal sealed class ShardingMigrator(
    IShardingRuntimeContext context,
    IMigrationsAssembly migrationsAssembly,
    IHistoryRepository historyRepository,
    IDatabaseCreator databaseCreator,
    IMigrationsSqlGenerator migrationsSqlGenerator,
    IRawSqlCommandBuilder rawSqlCommandBuilder,
    IMigrationCommandExecutor migrationCommandExecutor,
    IRelationalConnection connection,
    ISqlGenerationHelper sqlGenerationHelper,
    ICurrentDbContext currentContext,
    IModelRuntimeInitializer modelRuntimeInitializer,
    IDiagnosticsLogger<DbLoggerCategory.Migrations> logger,
    IRelationalCommandDiagnosticsLogger commandLogger,
    IDatabaseProvider databaseProvider,
    IMigrationsModelDiffer migrationsModelDiffer,
    IDesignTimeModel designTimeModel,
    IDbContextOptions dbContextOptions,
    IExecutionStrategy executionStrategy)
    : Migrator(
        migrationsAssembly,
        historyRepository,
        databaseCreator,
        migrationsSqlGenerator,
        rawSqlCommandBuilder,
        migrationCommandExecutor,
        connection,
        sqlGenerationHelper,
        currentContext,
        modelRuntimeInitializer,
        logger,
        commandLogger,
        databaseProvider,
        migrationsModelDiffer,
        designTimeModel,
        dbContextOptions,
        executionStrategy)
{
    public override void Migrate(string? targetMigration = null)
    {
        MigrateAsync(targetMigration).ConfigureAwait(false).GetAwaiter().GetResult();
        // base.Migrate(targetMigration);
    }

    private async Task ExecuteMigrateUnitsAsync(List<MigrateUnit> migrateUnits, string? targetMigration = null, CancellationToken cancellationToken = default)
    {
        var manager = context.MigrationManager;
        var migrateTasks = migrateUnits.Select(migrateUnit =>
        {
            return Task.Run(() =>
            {
                using (manager.CreateScope())
                {
                    manager.Current!.DataSource = migrateUnit.DataSource;
                    var options = context.CreateShellDbContextOptions(migrateUnit.DataSource);

                    using var ctx = context.RouteTailDbContextCreator.Create(migrateUnit.ShellDbContext, new ShardingDbContextOptions(options, context.RouteTailFactory.Create(string.Empty, false)));
                    if (targetMigration != null || ctx.Database.GetPendingMigrations().Any())
                    {
                        var migrator = ctx.GetService<IMigrator>();
                        migrator.Migrate(targetMigration);
                    }
                }

                return 1;

            }, cancellationToken);
        }).ToArray();
        await TaskHelper.WhenAllFastFail(migrateTasks);
    }

    public override async Task MigrateAsync(string? targetMigration = null, CancellationToken cancellationToken = default)
    {
        var allDataSource = context.VirtualDataSource.GetAllDataSource();
        var defaultDataSource = context.VirtualDataSource.DefaultDataSource;

        using var scope = context.ShardingProvider.CreateScope();
        using var shellDbContext = context.DbContextCreator.GetShell(scope);
        var parallelCount = context.Options.MigrationParallelCount;
        if (parallelCount <= 0)
        {
            throw new ShardingInvalidOperationException($"migration parallel count must >0");
        }

        //默认数据源需要最后执行 否则可能会导致异常的情况下GetPendingMigrations为空
        var partitionMigrationUnits = allDataSource.Where(o => o != defaultDataSource).Partition(parallelCount);
        foreach (var migrationUnits in partitionMigrationUnits)
        {
            var migrateUnits = migrationUnits.Select(o => new MigrateUnit(shellDbContext, o)).ToList();
            await ExecuteMigrateUnitsAsync(migrateUnits, targetMigration, cancellationToken);
        }

        //包含默认默认的单独最后一次处理
        if (allDataSource.Contains(defaultDataSource))
        {
            await ExecuteMigrateUnitsAsync([new(shellDbContext, defaultDataSource)], targetMigration, cancellationToken);
        }
    }

    public override string GenerateScript(string? fromMigration = null, string? toMigration = null,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        return new ShardingMigrationScriptGenerator(context, fromMigration, toMigration, options).GenerateScript();
    }
}

internal sealed class ShardingOptionsExtension(IShardingRuntimeContext context) : IDbContextOptionsExtension
{
    public IShardingRuntimeContext Context { get; } = context;

    public void ApplyServices(IServiceCollection services)
    {
        services.AddSingleton<IShardingRuntimeContext>(sp => Context);
    }

    public void Validate(IDbContextOptions options)
    {
    }

    public DbContextOptionsExtensionInfo Info => new ShardingOptionsExtensionInfo(this);

    private sealed class ShardingOptionsExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        private readonly ShardingOptionsExtension _extension = (ShardingOptionsExtension)extension;

        public override int GetServiceProviderHashCode()
        {
            return _extension.Context.GetHashCode();
        }

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
        {
            return true;
        }

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
        }

        public override bool IsDatabaseProvider => false;
        public override string LogFragment => nameof(ShardingOptionsExtension);
    }
}

internal sealed class ShardingWrapOptionsExtension : IDbContextOptionsExtension
{

    public ShardingWrapOptionsExtension()
    {
    }
    public void ApplyServices(IServiceCollection services)
    {
    }

    public void Validate(IDbContextOptions options)
    {
    }

    public DbContextOptionsExtensionInfo Info => new ShardingWrapDbContextOptionsExtensionInfo(this);

    private sealed class ShardingWrapDbContextOptionsExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override int GetServiceProviderHashCode()
        {
            return 0;
        }

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
        {
            return true;
        }

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
        }

        public override bool IsDatabaseProvider => false;

        public override string LogFragment => nameof(ShardingWrapOptionsExtension);
    }
}

internal sealed class ShardingRelationalTransaction(IShardingDbContext shardingDbContext, IRelationalConnection connection, DbTransaction transaction, Guid transactionId, IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger, bool transactionOwned, ISqlGenerationHelper sqlGenerationHelper) : RelationalTransaction(connection, transaction, transactionId, logger, transactionOwned, sqlGenerationHelper)
{
    private readonly IShardingDbContext _shardingDbContext = shardingDbContext ?? throw new ShardingInvalidOperationException($"should implement {nameof(IShardingDbContext)}");
    private readonly IShardingDbContextExecutor _executor = shardingDbContext.GetExecutor() ?? throw new ShardingInvalidOperationException($"{shardingDbContext.GetType()} cant get {nameof(IShardingDbContextExecutor)} from {nameof(shardingDbContext.GetExecutor)}");

    public override void Commit()
    {
        base.Commit();
        _executor.Commit();
        _executor.Notify();
    }

    public override void Rollback()
    {
        base.Rollback();
        _executor.Rollback();
        _executor.Notify();
    }

    public override async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        await base.RollbackAsync(cancellationToken);

        await _executor.RollbackAsync(cancellationToken);
        _executor.Notify();
    }

    public override async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await base.CommitAsync(cancellationToken);
        await _executor.CommitAsync(cancellationToken);
        _executor.Notify();
    }
    public override void CreateSavepoint(string name)
    {
        base.CreateSavepoint(name);
        _executor.CreateSavepoint(name);
    }

    public override async Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        await base.CreateSavepointAsync(name, cancellationToken);
        await _executor.CreateSavepointAsync(name, cancellationToken);
    }

    public override void RollbackToSavepoint(string name)
    {
        base.RollbackToSavepoint(name);
        _executor.RollbackToSavepoint(name);
    }

    public override async Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        await base.RollbackToSavepointAsync(name, cancellationToken);
        await _executor.RollbackToSavepointAsync(name, cancellationToken);
    }

    public override void ReleaseSavepoint(string name)
    {
        base.ReleaseSavepoint(name);
        _executor.ReleaseSavepoint(name);
    }

    public override async Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        await base.ReleaseSavepointAsync(name, cancellationToken);
        await _executor.ReleaseSavepointAsync(name, cancellationToken);
    }
}

internal sealed class ShardingRelationalTransactionFactory(RelationalTransactionFactoryDependencies dependencies) : RelationalTransactionFactory(dependencies)
{
    public override RelationalTransaction Create(IRelationalConnection connection, DbTransaction transaction, Guid transactionId, IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger, bool transactionOwned)
    {
        var shardingDbContext = connection.Context as IShardingDbContext
                                ?? throw new ShardingInvalidOperationException($"should implement {nameof(IShardingDbContext)}");
        return new ShardingRelationalTransaction(shardingDbContext, connection, transaction, transactionId, logger, transactionOwned, Dependencies.SqlGenerationHelper);
    }
}

internal sealed class ShardingRelationalTransactionManager : IRelationalTransactionManager
{
    private readonly IRelationalConnection _relationalConnection;
    private readonly IShardingDbContext _shardingDbContext;
    private readonly IShardingDbContextExecutor _shardingDbContextExecutor;
    public ShardingRelationalTransactionManager(IRelationalConnection relationalConnection)
    {
        _relationalConnection = relationalConnection;
        _shardingDbContext = relationalConnection.Context as IShardingDbContext ?? throw new ShardingInvalidOperationException($"should implement {nameof(IShardingDbContext)}");
        _shardingDbContextExecutor = _shardingDbContext.GetExecutor();
    }

    public void ResetState()
    {
        _relationalConnection.ResetState();
    }

    public IDbContextTransaction BeginTransaction()
    {
        return BeginTransaction(IsolationLevel.Unspecified);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return BeginTransactionAsync(IsolationLevel.Unspecified, cancellationToken);
    }

    public void CommitTransaction()
    {
        _relationalConnection.CommitTransaction();
    }

    public void RollbackTransaction()
    {
        _relationalConnection.RollbackTransaction();
    }

    public IDbContextTransaction? CurrentTransaction => _relationalConnection.CurrentTransaction;

    public IDbContextTransaction BeginTransaction(IsolationLevel isolationLevel)
    {
        var dbContextTransaction = _relationalConnection.BeginTransaction(isolationLevel);
        _shardingDbContextExecutor.Notify();
        return dbContextTransaction;
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        var dbContextTransaction = await _relationalConnection.BeginTransactionAsync(isolationLevel, cancellationToken);
        _shardingDbContextExecutor.Notify();
        return dbContextTransaction;
    }

    public IDbContextTransaction? UseTransaction(DbTransaction? transaction)
    {
        var dbContextTransaction = _relationalConnection.UseTransaction(transaction);
        _shardingDbContextExecutor.Notify();
        return dbContextTransaction;
    }

    public Task ResetStateAsync(CancellationToken cancellationToken = default)
    {
        return _relationalConnection.ResetStateAsync(cancellationToken);
    }

    public async Task<IDbContextTransaction?> UseTransactionAsync(DbTransaction? transaction, CancellationToken cancellationToken = default)
    {
        var dbContextTransaction = await _relationalConnection.UseTransactionAsync(transaction, cancellationToken);
        _shardingDbContextExecutor.Notify();
        return dbContextTransaction;
    }

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        return _relationalConnection.CommitTransactionAsync(cancellationToken);
    }
    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        return _relationalConnection.RollbackTransactionAsync(cancellationToken);
    }
    public IDbContextTransaction? UseTransaction(DbTransaction? transaction, Guid transactionId)
    {
        var dbContextTransaction = _relationalConnection.UseTransaction(transaction, transactionId);
        _shardingDbContextExecutor.Notify();
        return dbContextTransaction;
    }
    public async Task<IDbContextTransaction?> UseTransactionAsync(DbTransaction? transaction, Guid transactionId, CancellationToken cancellationToken = default)
    {
        var dbContextTransaction = await _relationalConnection.UseTransactionAsync(transaction, transactionId, cancellationToken);
        _shardingDbContextExecutor.Notify();
        return dbContextTransaction;
    }
}

internal sealed class ShardingDbSetInitializer(IDbSetFinder setFinder, IDbSetSource setSource) : DbSetInitializer(setFinder, setSource)
{
    public override void InitializeSets(DbContext context)
    {
        base.InitializeSets(context);
        if (context.IsShellDbContext())
        {
            context.GetRuntimeContext().GetOrCreate(context);
        }
    }
}

internal sealed class ShardingLocalView<T>(DbSet<T> set) : LocalView<T>(set)
    where T : class
{
    private readonly DbContext _dbContext = set.GetService<ICurrentDbContext>().Context;

    public override IEnumerator<T> GetEnumerator()
    {
        if (_dbContext is IShardingDbContext shardingDbContext && shardingDbContext.TryGetExecutor() is { } executor)
        {
            var enumerators = executor.GetAll()
                .SelectMany(o => o.Value.GetDbContext().Select(cd => cd.Value.Set<T>().Local.GetEnumerator()));
            return new MultipleEnumerator<T>(enumerators);
        }

        return base.GetEnumerator();
    }
}

/// <summary>
/// 壳 DbSet：Local 聚合各物理库已追踪实体。
/// </summary>
internal sealed class ShardingInternalDbSet<TEntity> : InternalDbSet<TEntity>
    where TEntity : class
{
    private LocalView<TEntity>? _localView;

    public ShardingInternalDbSet(DbContext context, string? entityTypeName)
        : base(context, entityTypeName)
    {
    }

    public override LocalView<TEntity> Local
        => _localView ??= new ShardingLocalView<TEntity>(this);
}

internal sealed class ShardingDbSetSource : IDbSetSource
{
    private static readonly MethodInfo GenericCreateSet =
        typeof(ShardingDbSetSource).GetTypeInfo().GetDeclaredMethod(nameof(CreateSetFactory))!;

    private readonly ConcurrentDictionary<(Type Type, string? Name), Func<DbContext, string?, object>> _cache = new();

    public object Create(DbContext context, Type type)
        => CreateCore(context, type, null);

    public object Create(DbContext context, string name, Type type)
        => CreateCore(context, type, name);

    private object CreateCore(DbContext context, Type type, string? name)
        => _cache.GetOrAdd(
            (type, name),
            static (t, createMethod) => (Func<DbContext, string?, object>)createMethod
                .MakeGenericMethod(t.Type)
                .Invoke(null, null)!,
            GenericCreateSet)(context, name);

    private static Func<DbContext, string?, object> CreateSetFactory<TEntity>()
        where TEntity : class
        => (c, name) => new ShardingInternalDbSet<TEntity>(c, name);
}

internal sealed class ShardingModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context)
    {
        return Create(context, false);
    }

    public object Create(DbContext context, bool designTime)
    {
        if (context is IShardingTableDbContext tableContext && tableContext.RouteTail != null && tableContext.RouteTail.Identity.IsNotNull())
        {

            return $"{context.GetType()}_{tableContext.RouteTail.Identity}_{designTime}";
        }
        else
        {
            return (context.GetType(), designTime);
        }
    }
}

internal sealed class ShardingModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        if (context is IShardingTableDbContext tableContext && tableContext.RouteTail != null && tableContext.RouteTail.ShardingTable)
        {
            var ctx = context.GetRuntimeContext();
            var metadataManager = ctx.EntityMetadataManager;
            var multiple = tableContext.RouteTail.MultipleQuery;
            if (!multiple)
            {
                var singleTail = (ISingleRouteTail)tableContext.RouteTail;
                var tail = singleTail.Tail;

                //设置分表
                var mutableTypes = modelBuilder.Model.GetEntityTypes().Where(o => metadataManager.IsShardingTable(o.ClrType)).ToArray();

                foreach (var entityType in mutableTypes)
                {
                    MappingToTable(metadataManager, entityType, modelBuilder, tail);
                }
            }
            else
            {
                var multipleTail = (IMultipleRouteTail)tableContext.RouteTail;
                var entityTypes = multipleTail.EntityTypes;
                var mutableTypes = modelBuilder.Model.GetEntityTypes().Where(o => metadataManager.IsShardingTable(o.ClrType) && entityTypes.Contains(o.ClrType)).ToArray();
                foreach (var entityType in mutableTypes)
                {
                    var queryTail = multipleTail.GetTail(entityType.ClrType);
                    if (queryTail != null)
                    {
                        MappingToTable(metadataManager, entityType, modelBuilder, queryTail);
                    }
                }
            }
        }
    }

    private static void MappingToTable(IEntityMetadataManager metadataManager, IMutableEntityType mutableType, ModelBuilder modelBuilder, string tail)
    {
        var clrType = mutableType.ClrType;
        var entityMetadata = metadataManager.TryGet(clrType);
        if (entityMetadata == null)
        {
            throw new ShardingInvalidOperationException($"not found entity type:[{clrType}]'s entity metadata");
        }

        if (entityMetadata.IsView)
        {
            throw new ShardingInvalidOperationException(
                $"entity type:[{clrType}]'s entity metadata is view cant remapping table name");
        }

        var shardingEntity = entityMetadata.EntityType;
        var tableSeparator = entityMetadata.TableSeparator;
        var entity = modelBuilder.Entity(shardingEntity);
        var tableName = entityMetadata.LogicTableName;
        if (tableName.IsNull())
        {
            throw new ArgumentNullException($"{shardingEntity}: not found logic table name。");
        }

        entity.ToTable($"{tableName}{tableSeparator}{tail}", entityMetadata.Schema);
    }
}

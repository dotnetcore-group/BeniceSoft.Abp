using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 同数据源下的DbContext管理者
/// </summary>
public interface IDataSourceDbContext : IShardingTransaction, IDisposable, IAsyncDisposable
{
    bool IsDefault { get; }

    int Count { get; }

    DbContext Create(IRouteTail routeTail);

    int SaveChanges(bool acceptAllChangesOnSuccess = true);

    Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess = true, CancellationToken cancellationToken = default);

    IDictionary<string, DbContext> GetDbContext();
}

internal sealed class DataSourceDbContext : IDataSourceDbContext
{
    private static readonly IComparer<string> _comparer = new NoShardingComparer();
    private readonly string _dataSource;
    private readonly DbContext _shell;
    private readonly IRouteTailDbContextCreator _creator;
    private readonly IShardingRuntimeContext _context;
    private readonly IVirtualDataSource _virtualDataSource;
    private readonly ActualConnectionStringManager _manager;
    private readonly SingleChecker _checker = new();

    /// <summary>
    /// 数据源排序默认提交将未分片的数据库最先提交
    /// </summary>
    private readonly SortedDictionary<string, DbContext> _dataSources = new(_comparer);

    /// <summary>
    /// 同库下共用一个db context options
    /// </summary>
    private DbContextOptions? _options;

    public DataSourceDbContext(string dataSource, bool isDefault, DbContext shell, IRouteTailDbContextCreator creator, ActualConnectionStringManager manager)
    {
        _dataSource = dataSource;
        IsDefault = isDefault;
        _shell = shell;
        _context = shell.GetRuntimeContext();
        _virtualDataSource = _context.VirtualDataSource;
        _creator = creator;
        _manager = manager;
    }

    private bool HasTransaction => _shell.Database.CurrentTransaction != null;

    private IDbContextTransaction? CurrentTransaction => IsDefault ? _shell.Database.CurrentTransaction : _dataSources.Values.FirstOrDefault(o => o.Database.CurrentTransaction != null)?.Database.CurrentTransaction;

    public bool IsDefault { get; }

    public int Count => _dataSources.Count;

    public void Commit()
    {
        if (IsDefault)
        {
            return;
        }

        CurrentTransaction?.Commit();
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (IsDefault)
        {
            return;
        }

        var transaction = CurrentTransaction;
        if (transaction != null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 创建共享的数据源配置用来做事务 不支持并发后期发现直接报错
    /// </summary>
    /// <returns></returns>
    private DbContextOptions CreateOptions()
    {
        if (_options != null)
        {
            return _options;
        }

        // 是否触发并发了
        var acquired = _checker.Start();
        if (!acquired)
        {
            throw new ShardingException("cant parallel create CreateShareDbContextOptionsBuilder");
        }

        try
        {
            // 先创建dbcontext option builder
            var creator = _context.DbContextOptionsBuilderCreator;
            var optionsBuilder = creator.Create(_shell).UseShardingOptions(_context);

            if (IsDefault)
            {
                // 如果是默认的需要使用shell的dbconnection为了保证可以使用事务
                var conn = _shell.Database.GetDbConnection();
                _virtualDataSource.UseDbContextOptionsBuilder(conn, optionsBuilder);
            }
            else
            {
                // 不同数据库：尚无物理 DbContext 时用连接串创建；已有则复用其 DbConnection（事务传播）
                if (_dataSources.Count == 0)
                {
                    var connectionString = _manager.GetConnectionString(_dataSource, true);
                    _virtualDataSource.UseDbContextOptionsBuilder(connectionString, optionsBuilder);
                    return optionsBuilder.Options;
                }

                var dbConnection = _dataSources.First().Value.Database.GetDbConnection();
                _virtualDataSource.UseDbContextOptionsBuilder(dbConnection, optionsBuilder);
            }

            _options = optionsBuilder.Options;
            return _options;
        }
        finally
        {
            _checker.Stop();
        }
    }

    public DbContext Create(IRouteTail routeTail)
    {
        if (routeTail.MultipleQuery)
        {
            throw new NotSupportedException("multi route not support track");
        }

        if (routeTail is not ISingleRouteTail)
        {
            throw new NotSupportedException("multi route not support track");
        }

        var cacheKey = routeTail.Identity;
        if (!_dataSources.TryGetValue(cacheKey, out var db))
        {
            db = _creator.Create(_shell, new ShardingDbContextOptions(CreateOptions(), routeTail));
            _dataSources.Add(cacheKey, db);
            BeginTransaction();
            JoinTransaction();
        }

        return db;
    }

    private void BeginTransaction()
    {
        if (!HasTransaction || IsDefault)
        {
            return;
        }

        if (_dataSources.Count == 0)
        {
            return;
        }

        var existing = _dataSources.Values.FirstOrDefault(o => o.Database.CurrentTransaction != null);
        if (existing != null)
        {
            return;
        }

        var level = IsolationLevel.ReadCommitted;
        if (TryGetShellDbTransaction(out var shellTx) && shellTx is not null)
        {
            level = shellTx.IsolationLevel;
        }

        _dataSources.First().Value.Database.BeginTransaction(level);
    }

    /// <summary>
    /// 加入到当前事务
    /// </summary>
    private void JoinTransaction()
    {
        if (!HasTransaction)
        {
            return;
        }

        if (!TryGetShellDbTransaction(out var dbTransaction) || dbTransaction is null)
        {
            return;
        }

        foreach (var db in _dataSources)
        {
            if (db.Value.Database.CurrentTransaction == null)
            {
                db.Value.Database.UseTransaction(dbTransaction);
            }
        }
    }

    private bool TryGetShellDbTransaction(out System.Data.Common.DbTransaction? dbTransaction)
    {
        dbTransaction = null;
        var tx = _shell.Database.CurrentTransaction;
        if (tx is null)
        {
            return false;
        }

        try
        {
            dbTransaction = tx.GetDbTransaction();
            return dbTransaction is not null;
        }
        catch (InvalidOperationException)
        {
            // ABP / Sharding 包装事务可能尚无底层 DbTransaction
            return false;
        }
    }

    /// <summary>
    /// 清理事务
    /// </summary>
    private void ClearTransaction()
    {
        foreach (var db in _dataSources)
        {
            if (db.Value.Database.CurrentTransaction != null)
            {
                db.Value.Database.UseTransaction(null);
            }
        }
    }

    public void CreateSavepoint(string name)
    {
        if (IsDefault)
        {
            return;
        }

        CurrentTransaction?.CreateSavepoint(name);
    }

    public async Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        if (IsDefault)
        {
            return;
        }

        var transaction = CurrentTransaction;
        if (transaction != null)
        {
            await transaction.CreateSavepointAsync(name, cancellationToken);
        }
    }

    public IDictionary<string, DbContext> GetDbContext()
    {
        return _dataSources;
    }

    public void Notify()
    {
        if (HasTransaction)
        {
            ClearTransaction();
        }
        else
        {
            BeginTransaction();
            JoinTransaction();
        }
    }

    public void ReleaseSavepoint(string name)
    {
        if (IsDefault)
        {
            return;
        }

        CurrentTransaction?.ReleaseSavepoint(name);
    }

    public async Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        if (IsDefault)
        {
            return;
        }

        var transaction = CurrentTransaction;
        if (transaction != null)
        {
            await transaction.ReleaseSavepointAsync(name, cancellationToken);
        }
    }

    public void Rollback()
    {
        if (IsDefault)
        {
            return;
        }

        CurrentTransaction?.Rollback();
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (IsDefault)
        {
            return;
        }

        var transaction = CurrentTransaction;
        if (transaction != null)
        {
            await transaction.RollbackAsync(cancellationToken);
        }
    }

    public void RollbackToSavepoint(string name)
    {
        if (IsDefault)
        {
            return;
        }

        CurrentTransaction?.RollbackToSavepoint(name);
    }

    public async Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        if (IsDefault)
        {
            return;
        }

        var transaction = CurrentTransaction;
        if (transaction != null)
        {
            await transaction.RollbackToSavepointAsync(name, cancellationToken);
        }
    }

    public int SaveChanges(bool acceptAllChangesOnSuccess = true)
    {
        var i = 0;
        foreach (var db in _dataSources)
        {
            i += db.Value.SaveChanges(acceptAllChangesOnSuccess);
        }

        return i;
    }

    public async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess = true, CancellationToken cancellationToken = default)
    {
        var i = 0;
        foreach (var db in _dataSources)
        {
            i += await db.Value.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        return i;
    }

    public void Dispose()
    {
        foreach (var db in _dataSources)
        {
            db.Value.Dispose();
        }

        _dataSources.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var db in _dataSources)
        {
            await db.Value.DisposeAsync();
        }

        _dataSources.Clear();
    }
}

internal sealed class ActualConnectionStringManager
{
    private readonly bool _useSeparation;
    private readonly ISeparationManager _manager;
    private readonly IVirtualDataSource _virtualDataSource;

    private readonly Dictionary<string, string> _connectionStrings = new(StringComparer.OrdinalIgnoreCase);

    public ActualConnectionStringManager(ISeparationManager manager, IVirtualDataSource virtualDataSource, DbContext shellDbContext)
    {
        ShellDbContext = shellDbContext;
        _manager = manager;
        _virtualDataSource = virtualDataSource;

        _useSeparation = virtualDataSource.ConnectionManager is SeparationConnectionManager;
        if (_useSeparation)
        {
            Priority = virtualDataSource.Options.SeparationPriority.GetValueOrDefault();
            Behavior = virtualDataSource.Options.SeparationBehavior;
            ReadStrategy = virtualDataSource.Options.ReadStrategy;
            ReadConnectionStrategy = virtualDataSource.Options.ReadConnectionStrategy;
        }
    }
    public DbContext ShellDbContext { get; }

    public int Priority { get; set; }

    public SeparationBehavior Behavior { get; set; }

    public SeparationReadStrategy ReadStrategy { get; set; }

    public SeparationReadConnectionStrategy ReadConnectionStrategy { get; set; }

    public string GetConnectionString(string dataSource, bool isWrite)
    {
        if (isWrite)
        {
            return GetWriteConnectionString(dataSource);
        }

        if (!_useSeparation)
        {
            return _virtualDataSource.ConnectionManager.GetConnectionString(dataSource);
        }
        else
        {
            return GetSeparationConnectString(dataSource);
        }
    }
    private string GetWriteConnectionString(string dataSourceName)
    {
        return _virtualDataSource.GetConnectionString(dataSourceName);
    }

    private static bool UseSeparation(SeparationBehavior behavior, bool inTransaction)
    {
        if (behavior == SeparationBehavior.Enable)
        {
            return true;
        }

        if (behavior == SeparationBehavior.OutTransaction)
        {
            return !inTransaction;
        }

        return false;
    }

    private string GetSeparationConnectString(string dataSource)
    {
        var inTrans = ShellDbContext.Database.CurrentTransaction != null;
        var support = UseSeparation(Behavior, inTrans);
        string? node = null;
        var has = false;
        var context = _manager.Current;
        if (context != null)
        {
            var dbFirst = Priority >= context.Priority;
            support = dbFirst ? UseSeparation(Behavior, inTrans) : UseSeparation(context.Behavior, inTrans);
            if (!dbFirst && support)
            {
                has = context.TryGetReadNode(dataSource, out node);
            }
        }

        if (support)
        {
            return GetSeparationConnectString(dataSource, has ? node : null);
        }

        return GetWriteConnectionString(dataSource);
    }

    private string GetSeparationConnectString(string dataSource, string? node)
    {
        if (_virtualDataSource.ConnectionManager is ISeparationConnectionManager
            manager)
        {
            if (ReadConnectionStrategy == SeparationReadConnectionStrategy.Cache)
            {
                if (!_connectionStrings.TryGetValue(dataSource, out var cached))
                {
                    cached = manager.GetReadNode(dataSource, node);
                    _connectionStrings[dataSource] = cached;
                }

                return cached;
            }
            else if (ReadConnectionStrategy == SeparationReadConnectionStrategy.Latest)
            {
                return manager.GetReadNode(dataSource, node);
            }
            else
            {
                throw new ShardingInvalidOperationException($"ReadWriteConnectionStringManager ReadConnectionStrategy:{ReadConnectionStrategy}");
            }
        }
        else
        {
            throw new ShardingInvalidOperationException($"virtual data source connection string manager is not [{nameof(ISeparationConnectionManager)}]");
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Data.Common;

namespace BeniceSoft.Abp.EntityFrameworkCore.Bulk;

/// <summary>
/// 多步批量写的共享会话：多次 Insert/Update/Delete/Merge 共用同一连接与事务。
/// <para>
/// <b>原理：</b>与一次性 <see cref="EfCoreBulkAtom{T}"/> 不同，这里不会在每步后自动提交。
/// 适合「先插一批、再更新一批」需要原子性的场景；结束后必须显式 <see cref="Commit"/> / <see cref="Rollback"/>。
/// Dispose 时仅释放本对象自开的事务/连接，不会替你提交
/// </para>
/// </summary>
public abstract class EfCoreBulkOperation : IAsyncDisposable, IDisposable
{
    private readonly DbContext _ctx;
    private DbConnection? _connection;
    private DbTransaction? _transaction;
    private bool _ownConnection;
    private bool _ownTransaction;
    private bool _disposed;

    protected DbContext DbContext => _ctx;

    protected EfCoreBulkOperation(DbContext ctx)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
    }

    /// <summary>在当前会话事务内执行批量插入（不自动提交）</summary>
    public int BulkInsert<T>(IEnumerable<T> items, Action<EfCoreBulkAtom<T>>? tableBuilder = null)
        where T : class
    {
        Initialize();
        var atom = CreateAtom(items);
        tableBuilder?.Invoke(atom);
        return atom.BulkInsert(_connection!, _transaction!);
    }

    /// <inheritdoc cref="BulkInsert{T}"/>
    public Task<int> BulkInsertAsync<T>(IEnumerable<T> items, Action<EfCoreBulkAtom<T>>? tableBuilder = null, CancellationToken cancellationToken = default)
        where T : class
    {
        Initialize();
        var atom = CreateAtom(items);
        tableBuilder?.Invoke(atom);
        return atom.BulkInsertAsync(_connection!, _transaction!, cancellationToken);
    }

    /// <summary>在当前会话事务内按匹配列批量删除（不自动提交）。</summary>
    public int BulkDelete<T>(IEnumerable<T> items, Action<EfCoreBulkAtom<T>>? tableBuilder = null, Action<BulkMatchOptions<T>>? matchBuilder = null)
        where T : class
    {
        Initialize();
        var atom = CreateAtom(items);
        tableBuilder?.Invoke(atom);
        return atom.BulkDelete(_connection!, _transaction!, matchBuilder);
    }

    /// <inheritdoc cref="BulkDelete{T}"/>
    public Task<int> BulkDeleteAsync<T>(IEnumerable<T> items, Action<EfCoreBulkAtom<T>>? tableBuilder = null, Action<BulkMatchOptions<T>>? matchBuilder = null, CancellationToken cancellationToken = default)
        where T : class
    {
        Initialize();
        var atom = CreateAtom(items);
        tableBuilder?.Invoke(atom);
        return atom.BulkDeleteAsync(_connection!, _transaction!, matchBuilder, cancellationToken);
    }

    /// <summary>在当前会话事务内按匹配列批量更新（不自动提交）。</summary>
    public int BulkUpdate<T>(IEnumerable<T> items, Action<EfCoreBulkAtom<T>>? tableBuilder = null, Action<BulkMatchOptions<T>>? matchBuilder = null)
        where T : class
    {
        Initialize();
        var atom = CreateAtom(items);
        tableBuilder?.Invoke(atom);
        return atom.BulkUpdate(_connection!, _transaction!, matchBuilder);
    }

    /// <inheritdoc cref="BulkUpdate{T}"/>
    public Task<int> BulkUpdateAsync<T>(IEnumerable<T> items, Action<EfCoreBulkAtom<T>>? tableBuilder = null, Action<BulkMatchOptions<T>>? matchBuilder = null, CancellationToken cancellationToken = default)
        where T : class
    {
        Initialize();
        var atom = CreateAtom(items);
        tableBuilder?.Invoke(atom);
        return atom.BulkUpdateAsync(_connection!, _transaction!, matchBuilder, cancellationToken);
    }

    /// <summary>在当前会话事务内 Upsert（不自动提交）。</summary>
    public int BulkMerge<T>(IEnumerable<T> items, Action<EfCoreBulkAtom<T>>? tableBuilder = null, Action<BulkMatchOptions<T>>? matchBuilder = null)
        where T : class
    {
        Initialize();
        var atom = CreateAtom(items);
        tableBuilder?.Invoke(atom);
        return atom.BulkMerge(_connection!, _transaction!, matchBuilder);
    }

    /// <inheritdoc cref="BulkMerge{T}"/>
    public Task<int> BulkMergeAsync<T>(IEnumerable<T> items, Action<EfCoreBulkAtom<T>>? tableBuilder = null, Action<BulkMatchOptions<T>>? matchBuilder = null, CancellationToken cancellationToken = default)
        where T : class
    {
        Initialize();
        var atom = CreateAtom(items);
        tableBuilder?.Invoke(atom);
        return atom.BulkMergeAsync(_connection!, _transaction!, matchBuilder, cancellationToken);
    }

    /// <summary>提交本对象自开的事务；若复用外部事务则 no-op（由外部 UoW 提交）。</summary>
    public void Commit()
    {
        if (_ownTransaction)
        {
            _transaction?.Commit();
        }
    }

    /// <inheritdoc cref="Commit"/>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_ownTransaction && _transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
        }
    }

    /// <summary>回滚本对象自开的事务；复用外部事务时 no-op。</summary>
    public void Rollback()
    {
        if (_ownTransaction)
        {
            _transaction?.Rollback();
        }
    }

    /// <inheritdoc cref="Rollback"/>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_ownTransaction && _transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
    }

    /// <summary>由提供程序创建具体 Atom（SqlServer / Npgsql）。</summary>
    protected abstract EfCoreBulkAtom<T> CreateAtom<T>(IEnumerable<T> items)
        where T : class;

    /// <summary>
    /// 懒初始化：第一次调用任意 Bulk* 时打开连接并绑定事务，后续步骤复用，保证多步在同一事务边界内。
    /// </summary>
    private void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection == null)
        {
            _connection = _ctx.Database.GetDbConnection();
            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
                _ownConnection = true;
            }
        }
        else if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }

        if (_transaction != null)
        {
            return;
        }

        var entityTransaction = _ctx.Database.GetService<IRelationalConnection>().CurrentTransaction?.GetDbTransaction();
        if (entityTransaction == null)
        {
            _transaction = _connection.BeginTransaction();
            _ownTransaction = true;
            return;
        }

        _transaction = entityTransaction;
        _ownTransaction = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownTransaction)
        {
            _transaction?.Dispose();
        }

        if (_ownConnection && _connection is { State: not ConnectionState.Closed })
        {
            _connection.Close();
        }

        _transaction = null;
        _connection = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownTransaction && _transaction != null)
        {
            await _transaction.DisposeAsync();
        }

        if (_ownConnection && _connection is { State: not ConnectionState.Closed })
        {
            await _connection.CloseAsync();
        }

        _transaction = null;
        _connection = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

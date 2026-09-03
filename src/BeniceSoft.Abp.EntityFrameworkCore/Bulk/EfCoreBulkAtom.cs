using BeniceSoft.Core;
using BeniceSoft.Core.Reflector;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text;

namespace BeniceSoft.Abp.EntityFrameworkCore.Bulk;

/// <summary>
/// 批量写操作基类
/// <para>
/// <b>设计原理：</b>不走 EF ChangeTracker / SaveChanges，而是把实体映射成原生 bulk 通道所需的列数据，
/// 由 SqlServer（SqlBulkCopy + MERGE）或 PostgreSQL（COPY + UPDATE/DELETE/ON CONFLICT）子类完成真正落库。
/// 因此不会触发 AuditTrail、领域事件等 SaveChanges 管道；如需审计请自行处理。
/// </para>
/// <para>
/// <b>事务约定：</b>若 DbContext 当前无事务，一次性 API 会自开自提交；若已有事务（如 ABP UoW），则复用外部事务，由调用方提交。
/// </para>
/// </summary>
public abstract class EfCoreBulkAtom<T> where T : class
{
    protected EfCoreBulkAtom(DbContext ctx, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(items);

        // 从 EF 模型解析表名、Schema 与属性→列映射，后续 bulk 完全依赖这套元数据，避免手写表映射
        var entityType = ctx.Model.FindEntityType(typeof(T))
                         ?? throw new InvalidDataException("no tables of this type were found in the database");

        DbContext = ctx;
        Items = items is ICollection<T> collection ? collection : items.ToList();
        Options.Schema = entityType.GetSchema();
        Options.TableName = entityType.GetTableName();
        EntityType = entityType;
        ColumnMappings = entityType.GetTableMappings().First().ColumnMappings.ToList();
    }

    /// <summary>EF 实体元数据（主键、表映射等）</summary>
    protected internal IEntityType EntityType { get; }

    public DbContext DbContext { get; }

    /// <summary>当前参与 bulk 的属性→数据库列映射；可通过 <see cref="RemoveColumn"/> 剔除不需要写入的列</summary>
    public List<Microsoft.EntityFrameworkCore.Metadata.IColumnMapping> ColumnMappings { get; }

    public IEnumerable<T> Items { get; }

    public BulkOptions Options { get; } = new();
    public EfCoreBulkAtom<T> WithCommandTimeout(int seconds)
    {
        Options.CommandTimeout = seconds;
        return this;
    }

    public EfCoreBulkAtom<T> WithBulkCopyTimeout(int seconds)
    {
        Options.BulkCopyTimeout = seconds;
        return this;
    }

    public EfCoreBulkAtom<T> WithBulkCopyEnableStreaming(bool status)
    {
        Options.BulkCopyEnableStreaming = status;
        return this;
    }

    public EfCoreBulkAtom<T> WithBulkCopyNotifyAfter(int rows)
    {
        Options.BulkCopyNotifyAfter = rows;
        return this;
    }

    public EfCoreBulkAtom<T> WithBulkCopyBatchSize(int rows)
    {
        Options.BulkCopyBatchSize = rows;
        return this;
    }

    /// <summary>从 bulk 列集中移除指定属性（例如自增列、数据库默认值列）</summary>
    public EfCoreBulkAtom<T> RemoveColumn(Expression<Func<T, object>> keyExpression)
    {
        var properties = keyExpression.GetProperties().Select(t => t.Name).ToHashSet();
        ColumnMappings.RemoveAll(t => properties.Contains(t.Property.Name));
        return this;
    }

    /// <summary>批量插入：一次性操作，按事务约定自动提交或复用外部事务</summary>
    public int BulkInsert()
    {
        var (conn, trans, ownConnection, ownTrans) = GetConnection();
        try
        {
            var result = BulkInsert(conn, trans);
            Commit(conn, trans, ownConnection, ownTrans);
            return result;
        }
        catch
        {
            if (ownTrans)
            {
                trans.Rollback();
            }

            throw;
        }
        finally
        {
            if (ownConnection && conn.State != ConnectionState.Closed)
            {
                conn.Close();
            }
        }
    }

    /// <inheritdoc cref="BulkInsert()"/>
    public async Task<int> BulkInsertAsync(CancellationToken cancellationToken = default)
    {
        var (conn, trans, ownConnection, ownTrans) = await GetConnectionAsync(cancellationToken);
        try
        {
            var result = await BulkInsertAsync(conn, trans, cancellationToken);
            await CommitAsync(conn, trans, ownConnection, ownTrans, cancellationToken);
            return result;
        }
        catch
        {
            if (ownTrans)
            {
                await trans.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (ownConnection && conn.State != ConnectionState.Closed)
            {
                await conn.CloseAsync();
            }
        }
    }

    /// <summary>批量删除：按匹配列（默认主键）删除目标表中已存在的行。</summary>
    public int BulkDelete(Action<BulkMatchOptions<T>>? setup = null)
    {
        return ExecuteOneShot((c, t) => BulkDelete(c, t, setup));
    }

    /// <inheritdoc cref="BulkDelete(System.Action{BulkMatchOptions{T}})"/>
    public Task<int> BulkDeleteAsync(Action<BulkMatchOptions<T>>? setup = null, CancellationToken cancellationToken = default)
    {
        return ExecuteOneShotAsync((c, t, ct) => BulkDeleteAsync(c, t, setup, ct), cancellationToken);
    }

    /// <summary>批量更新：按匹配列定位行，用内存实体覆盖非匹配列。</summary>
    public int BulkUpdate(Action<BulkMatchOptions<T>>? setup = null)
    {
        return ExecuteOneShot((c, t) => BulkUpdate(c, t, setup));
    }

    /// <inheritdoc cref="BulkUpdate(System.Action{BulkMatchOptions{T}})"/>
    public Task<int> BulkUpdateAsync(Action<BulkMatchOptions<T>>? setup = null, CancellationToken cancellationToken = default)
    {
        return ExecuteOneShotAsync((c, t, ct) => BulkUpdateAsync(c, t, setup, ct), cancellationToken);
    }

    /// <summary>
    /// 批量合并（Upsert）：匹配则更新，不匹配则插入。
    /// 匹配列须对应目标表唯一约束/主键（PG 的 ON CONFLICT 尤其依赖这一点）。
    /// </summary>
    public int BulkMerge(Action<BulkMatchOptions<T>>? setup = null)
    {
        return ExecuteOneShot((c, t) => BulkMerge(c, t, setup));
    }

    /// <inheritdoc cref="BulkMerge(System.Action{BulkMatchOptions{T}})"/>
    public Task<int> BulkMergeAsync(Action<BulkMatchOptions<T>>? setup = null, CancellationToken cancellationToken = default)
    {
        return ExecuteOneShotAsync((c, t, ct) => BulkMergeAsync(c, t, setup, ct), cancellationToken);
    }

    /// <summary>提供程序实现：真正的 Insert / Update / Delete / Merge。</summary>
    protected internal abstract int BulkInsert(DbConnection connection, DbTransaction transaction);

    protected internal abstract Task<int> BulkInsertAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);

    protected internal abstract int BulkDelete(DbConnection connection, DbTransaction transaction, Action<BulkMatchOptions<T>>? setup);

    protected internal abstract Task<int> BulkDeleteAsync(DbConnection connection, DbTransaction transaction, Action<BulkMatchOptions<T>>? setup, CancellationToken cancellationToken);

    protected internal abstract int BulkUpdate(DbConnection connection, DbTransaction transaction, Action<BulkMatchOptions<T>>? setup);

    protected internal abstract Task<int> BulkUpdateAsync(DbConnection connection, DbTransaction transaction, Action<BulkMatchOptions<T>>? setup, CancellationToken cancellationToken);

    protected internal abstract int BulkMerge(DbConnection connection, DbTransaction transaction, Action<BulkMatchOptions<T>>? setup);

    protected internal abstract Task<int> BulkMergeAsync(DbConnection connection, DbTransaction transaction, Action<BulkMatchOptions<T>>? setup, CancellationToken cancellationToken);

    /// <summary>标识符引号规则：SQL Server 用 []，PostgreSQL 用 ""。</summary>
    protected abstract string QuoteIdentifier(string name);
    protected string GetQuotedTableName()
    {
        var sb = new StringBuilder();
        if (Options.Schema.IsNotNull())
        {
            sb.Append(QuoteIdentifier(Options.Schema)).Append('.');
        }

        sb.Append(QuoteIdentifier(Options.TableName!));
        return sb.ToString();
    }

    /// <summary>
    /// 将内存实体投影为 DataTable，供 SqlBulkCopy 等按列名写入。
    /// 列集合以 <see cref="ColumnMappings"/> 为准，可先 RemoveColumn 再构建。
    /// </summary>
    protected DataTable BuildDataTable()
    {
        var dt = new DataTable(Options.TableName);
        foreach (var column in ColumnMappings)
        {
            var colType = column.Property.ClrType.GetUnderlyingType();
            dt.Columns.Add(column.Column.Name, colType);
        }

        foreach (var item in Items)
        {
            var dr = dt.NewRow();
            foreach (var column in ColumnMappings)
            {
                var value = column.Property.PropertyInfo?.GetReflector().GetValue(item);
                dr[column.Column.Name] = value ?? DBNull.Value;
            }

            dt.Rows.Add(dr);
        }

        return dt;
    }

    /// <summary>
    /// 获取连接与事务。
    /// <list type="bullet">
    /// <item>已有 EF 环境事务 → 复用（OwnTransaction=false，由外部提交）。</item>
    /// <item>无事务 → 新建（OwnTransaction=true，一次性 API 结束后自动 Commit）。</item>
    /// </list>
    /// </summary>
    protected (DbConnection Connection, DbTransaction Transaction, bool OwnConnection, bool OwnTransaction) GetConnection()
    {
        var conn = DbContext.Database.GetDbConnection();
        var ownConnection = false;
        if (conn.State != ConnectionState.Open)
        {
            conn.Open();
            ownConnection = true;
        }

        var entityTransaction = DbContext.Database.GetService<IRelationalConnection>().CurrentTransaction?.GetDbTransaction();
        if (entityTransaction != null)
        {
            return (conn, entityTransaction, ownConnection, false);
        }

        return (conn, conn.BeginTransaction(), ownConnection, true);
    }
    protected async Task<(DbConnection Connection, DbTransaction Transaction, bool OwnConnection, bool OwnTransaction)> GetConnectionAsync(CancellationToken cancellationToken)
    {
        var conn = DbContext.Database.GetDbConnection();
        var ownConnection = false;
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(cancellationToken);
            ownConnection = true;
        }

        var entityTransaction = DbContext.Database.GetService<IRelationalConnection>().CurrentTransaction?.GetDbTransaction();
        if (entityTransaction != null)
        {
            return (conn, entityTransaction, ownConnection, false);
        }

        return (conn, await conn.BeginTransactionAsync(cancellationToken), ownConnection, true);
    }

    /// <summary>一次性 bulk：执行 →（自有事务则）提交 → 关闭自开连接；失败时回滚自有事务。</summary>
    private int ExecuteOneShot(Func<DbConnection, DbTransaction, int> action)
    {
        var (conn, trans, ownConnection, ownTrans) = GetConnection();
        try
        {
            var result = action(conn, trans);
            Commit(conn, trans, ownConnection, ownTrans);
            return result;
        }
        catch
        {
            if (ownTrans)
            {
                trans.Rollback();
            }

            throw;
        }
        finally
        {
            if (ownConnection && conn.State != ConnectionState.Closed)
            {
                conn.Close();
            }
        }
    }

    private async Task<int> ExecuteOneShotAsync(Func<DbConnection, DbTransaction, CancellationToken, Task<int>> action, CancellationToken cancellationToken)
    {
        var (conn, trans, ownConnection, ownTrans) = await GetConnectionAsync(cancellationToken);
        try
        {
            var result = await action(conn, trans, cancellationToken);
            await CommitAsync(conn, trans, ownConnection, ownTrans, cancellationToken);
            return result;
        }
        catch
        {
            if (ownTrans)
            {
                await trans.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (ownConnection && conn.State != ConnectionState.Closed)
            {
                await conn.CloseAsync();
            }
        }
    }

    private static void Commit(DbConnection connection, DbTransaction transaction, bool ownConnection, bool ownTrans)
    {
        if (ownTrans)
        {
            transaction.Commit();
            transaction.Dispose();
        }

        // connection closed by caller finally when ownConnection
        _ = connection;
        _ = ownConnection;
    }

    private static async Task CommitAsync(DbConnection connection, DbTransaction transaction, bool ownConnection, bool ownTrans, CancellationToken cancellationToken)
    {
        if (ownTrans)
        {
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
        }

        _ = connection;
        _ = ownConnection;
    }
}

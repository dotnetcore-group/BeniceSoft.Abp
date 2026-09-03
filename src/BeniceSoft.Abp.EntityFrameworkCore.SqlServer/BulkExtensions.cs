using BeniceSoft.Abp.EntityFrameworkCore.Bulk;
using BeniceSoft.Abp.EntityFrameworkCore.SqlServer.Bulk;
using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore.SqlServer;

/// <summary>
/// SQL Server Bulk 入口扩展
/// <para>
/// <b>两种用法：</b><br/>
/// ① 一次性：<c>ctx.BulkInsert/Update/Delete/Merge(...)</c> —— 内部建 Atom，按事务约定自开自提交或复用 UoW<br/>
/// ② 多步会话：<c>using var op = ctx.BulkOperation()</c> —— 多步共用同一事务，需显式 Commit
/// </para>
/// </summary>
public static class BulkExtensions
{
    /// <summary>开启多步 bulk 会话（共享连接/事务）。</summary>
    public static SqlServerBulkOperation BulkOperation(this DbContext ctx)
        => new(ctx);

    /// <summary>创建可配置的一次性 Atom（可先 RemoveColumn / 调超时，再调用 Bulk*）</summary>
    public static SqlServerBulkAtom<T> BulkAtom<T>(this DbContext ctx, IEnumerable<T> items)
        where T : class
        => new(ctx, items);

    /// <summary>SqlBulkCopy 直写目标表</summary>
    public static int BulkInsert<T>(this DbContext ctx, IEnumerable<T> items, Action<SqlServerBulkAtom<T>>? tableBuilder = null)
        where T : class
    {
        var bulk = ctx.BulkAtom(items);
        tableBuilder?.Invoke(bulk);
        return bulk.BulkInsert();
    }

    /// <inheritdoc cref="BulkInsert{T}"/>
    public static Task<int> BulkInsertAsync<T>(this DbContext ctx, IEnumerable<T> items, Action<SqlServerBulkAtom<T>>? tableBuilder = null, CancellationToken cancellationToken = default)
        where T : class
    {
        var bulk = ctx.BulkAtom(items);
        tableBuilder?.Invoke(bulk);
        return bulk.BulkInsertAsync(cancellationToken);
    }

    /// <summary>#TmpTable + MERGE WHEN MATCHED THEN DELETE</summary>
    public static int BulkDelete<T>(this DbContext ctx, IEnumerable<T> items, Action<SqlServerBulkAtom<T>>? tableBuilder = null, Action<BulkMatchOptions<T>>? matchBuilder = null)
        where T : class
    {
        var bulk = ctx.BulkAtom(items);
        tableBuilder?.Invoke(bulk);
        return bulk.BulkDelete(matchBuilder);
    }

    /// <inheritdoc cref="BulkDelete{T}"/>
    public static Task<int> BulkDeleteAsync<T>(this DbContext ctx, IEnumerable<T> items, Action<SqlServerBulkAtom<T>>? tableBuilder = null, Action<BulkMatchOptions<T>>? matchBuilder = null, CancellationToken cancellationToken = default)
        where T : class
    {
        var bulk = ctx.BulkAtom(items);
        tableBuilder?.Invoke(bulk);
        return bulk.BulkDeleteAsync(matchBuilder, cancellationToken);
    }

    /// <summary>#TmpTable + MERGE WHEN MATCHED THEN UPDATE。</summary>
    public static int BulkUpdate<T>(this DbContext ctx, IEnumerable<T> items, Action<SqlServerBulkAtom<T>>? tableBuilder = null, Action<BulkMatchOptions<T>>? matchBuilder = null)
        where T : class
    {
        var bulk = ctx.BulkAtom(items);
        tableBuilder?.Invoke(bulk);
        return bulk.BulkUpdate(matchBuilder);
    }

    /// <inheritdoc cref="BulkUpdate{T}"/>
    public static Task<int> BulkUpdateAsync<T>(this DbContext ctx, IEnumerable<T> items, Action<SqlServerBulkAtom<T>>? tableBuilder = null, Action<BulkMatchOptions<T>>? matchBuilder = null, CancellationToken cancellationToken = default)
        where T : class
    {
        var bulk = ctx.BulkAtom(items);
        tableBuilder?.Invoke(bulk);
        return bulk.BulkUpdateAsync(matchBuilder, cancellationToken);
    }

    /// <summary>#TmpTable + MERGE Upsert（匹配更新，不匹配插入）。</summary>
    public static int BulkMerge<T>(this DbContext ctx, IEnumerable<T> items, Action<SqlServerBulkAtom<T>>? tableBuilder = null, Action<BulkMatchOptions<T>>? matchBuilder = null)
        where T : class
    {
        var bulk = ctx.BulkAtom(items);
        tableBuilder?.Invoke(bulk);
        return bulk.BulkMerge(matchBuilder);
    }

    /// <inheritdoc cref="BulkMerge{T}"/>
    public static Task<int> BulkMergeAsync<T>(this DbContext ctx, IEnumerable<T> items, Action<SqlServerBulkAtom<T>>? tableBuilder = null, Action<BulkMatchOptions<T>>? matchBuilder = null, CancellationToken cancellationToken = default)
        where T : class
    {
        var bulk = ctx.BulkAtom(items);
        tableBuilder?.Invoke(bulk);
        return bulk.BulkMergeAsync(matchBuilder, cancellationToken);
    }
}

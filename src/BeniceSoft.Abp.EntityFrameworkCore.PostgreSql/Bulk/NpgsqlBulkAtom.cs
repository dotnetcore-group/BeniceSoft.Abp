using System.Data.Common;
using System.Text;
using BeniceSoft.Abp.EntityFrameworkCore.Bulk;
using BeniceSoft.Core.Reflector;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BeniceSoft.Abp.EntityFrameworkCore.PostgreSql.Bulk;

/// <summary>
/// PostgreSQL 批量写实现。
/// <para>
/// <b>Insert：</b><c>COPY ... FROM STDIN (FORMAT BINARY)</c>（Npgsql BinaryImporter），等价于 PG 侧最高效的装载路径。<br/>
/// <b>Update / Delete / Merge：</b>
/// ① <c>CREATE TEMP TABLE</c> → ② COPY 灌入源数据 → ③
/// Update 用 <c>UPDATE ... FROM temp</c>，Delete 用 <c>DELETE ... USING temp</c>，
/// Merge 用 <c>INSERT ... ON CONFLICT (...) DO UPDATE</c> → ④ DROP TEMP。
/// 匹配列必须落在唯一约束/主键上，否则 ON CONFLICT 无法使用。
/// </para>
/// </summary>
public class NpgsqlBulkAtom<T> : EfCoreBulkAtom<T>
    where T : class
{
    /// <summary>当前会话临时表名（ON COMMIT DROP，事务结束自动消失）。</summary>
    private const string TmpTable = "tmp_bulk";

    public NpgsqlBulkAtom(DbContext ctx, IEnumerable<T> items)
        : base(ctx, items)
    {
    }

    public new NpgsqlBulkAtom<T> WithCommandTimeout(int seconds)
    {
        base.WithCommandTimeout(seconds);
        return this;
    }

    public new NpgsqlBulkAtom<T> WithBulkCopyTimeout(int seconds)
    {
        base.WithBulkCopyTimeout(seconds);
        return this;
    }

    public new NpgsqlBulkAtom<T> WithBulkCopyBatchSize(int rows)
    {
        base.WithBulkCopyBatchSize(rows);
        return this;
    }

    public new NpgsqlBulkAtom<T> RemoveColumn(System.Linq.Expressions.Expression<Func<T, object>> keyExpression)
    {
        base.RemoveColumn(keyExpression);
        return this;
    }

    protected override string QuoteIdentifier(string name) => $"\"{name}\"";

    /// <summary>COPY 二进制直写目标表。</summary>
    protected override int BulkInsert(DbConnection connection, DbTransaction transaction)
    {
        var (conn, _) = Cast(connection, transaction);
        BinaryImport(conn, GetQuotedTableName());
        return Items.Count();
    }

    protected override async Task<int> BulkInsertAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var (conn, _) = Cast(connection, transaction);
        await BinaryImportAsync(conn, GetQuotedTableName(), cancellationToken);
        return Items.Count();
    }

    protected override int BulkDelete(DbConnection connection, DbTransaction transaction, Action<BulkMatchOptions<T>>? setup)
        => ExecuteTempPath(connection, transaction, setup, BuildDeleteSql);

    protected override Task<int> BulkDeleteAsync(DbConnection connection, DbTransaction transaction, Action<BulkMatchOptions<T>>? setup, CancellationToken cancellationToken)
        => ExecuteTempPathAsync(connection, transaction, setup, BuildDeleteSql, cancellationToken);

    protected override int BulkUpdate(DbConnection connection, DbTransaction transaction, Action<BulkMatchOptions<T>>? setup)
        => ExecuteTempPath(connection, transaction, setup, BuildUpdateSql);

    protected override Task<int> BulkUpdateAsync(DbConnection connection, DbTransaction transaction, Action<BulkMatchOptions<T>>? setup, CancellationToken cancellationToken)
        => ExecuteTempPathAsync(connection, transaction, setup, BuildUpdateSql, cancellationToken);

    protected override int BulkMerge(DbConnection connection, DbTransaction transaction, Action<BulkMatchOptions<T>>? setup)
        => ExecuteTempPath(connection, transaction, setup, BuildMergeSql);

    protected override Task<int> BulkMergeAsync(DbConnection connection, DbTransaction transaction, Action<BulkMatchOptions<T>>? setup, CancellationToken cancellationToken)
        => ExecuteTempPathAsync(connection, transaction, setup, BuildMergeSql, cancellationToken);

    /// <summary>
    /// Update/Delete/Merge 共用：先把源数据集中进 TEMP，再一条集合 SQL 对齐正式表。
    /// 比「客户端循环 Execute」少往返，也比巨型 VALUES 列表更稳。
    /// </summary>
    private int ExecuteTempPath(
        DbConnection connection,
        DbTransaction transaction,
        Action<BulkMatchOptions<T>>? setup,
        Func<IList<string>, BulkMatchOptions<T>, string> buildSql)
    {
        var (conn, trans) = Cast(connection, transaction);
        var match = new BulkMatchOptions<T>(this);
        setup?.Invoke(match);
        var updateOn = match.GetMatchColumns();

        using var command = conn.CreateCommand();
        command.Transaction = trans;
        command.CommandTimeout = Options.CommandTimeout;

        // ① 会话临时表，结构取自 EF 列 StoreType。
        command.CommandText = BuildTempTableSql();
        command.ExecuteNonQuery();

        // ② Binary COPY 灌源数据。
        BinaryImport(conn, TmpTable);

        // ③ 集合 SQL：UPDATE FROM / DELETE USING / INSERT ON CONFLICT。
        command.CommandText = buildSql(updateOn, match);
        var result = command.ExecuteNonQuery();

        command.CommandText = $"DROP TABLE IF EXISTS {TmpTable};";
        command.ExecuteNonQuery();
        return result;
    }

    private async Task<int> ExecuteTempPathAsync(
        DbConnection connection,
        DbTransaction transaction,
        Action<BulkMatchOptions<T>>? setup,
        Func<IList<string>, BulkMatchOptions<T>, string> buildSql,
        CancellationToken cancellationToken)
    {
        var (conn, trans) = Cast(connection, transaction);
        var match = new BulkMatchOptions<T>(this);
        setup?.Invoke(match);
        var updateOn = match.GetMatchColumns();

        await using var command = conn.CreateCommand();
        command.Transaction = trans;
        command.CommandTimeout = Options.CommandTimeout;
        command.CommandText = BuildTempTableSql();
        await command.ExecuteNonQueryAsync(cancellationToken);

        await BinaryImportAsync(conn, TmpTable, cancellationToken);

        command.CommandText = buildSql(updateOn, match);
        var result = await command.ExecuteNonQueryAsync(cancellationToken);

        command.CommandText = $"DROP TABLE IF EXISTS {TmpTable};";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Npgsql BinaryImporter：客户端按行写入二进制流，服务端以 COPY 协议落表，避免文本解析与逐参绑定。
    /// </summary>
    private void BinaryImport(NpgsqlConnection connection, string destinationTable)
    {
        var columns = string.Join(", ", ColumnMappings.Select(m => QuoteIdentifier(m.Column.Name)));
        var copySql = $"COPY {destinationTable} ({columns}) FROM STDIN (FORMAT BINARY)";
        using var writer = connection.BeginBinaryImport(copySql);
        WriteRows(writer);
        writer.Complete();
    }

    private async Task BinaryImportAsync(NpgsqlConnection connection, string destinationTable, CancellationToken cancellationToken)
    {
        var columns = string.Join(", ", ColumnMappings.Select(m => QuoteIdentifier(m.Column.Name)));
        var copySql = $"COPY {destinationTable} ({columns}) FROM STDIN (FORMAT BINARY)";
        await using var writer = await connection.BeginBinaryImportAsync(copySql, cancellationToken);
        await WriteRowsAsync(writer, cancellationToken);
        await writer.CompleteAsync(cancellationToken);
    }

    private void WriteRows(NpgsqlBinaryImporter writer)
    {
        foreach (var item in Items)
        {
            writer.StartRow();
            foreach (var mapping in ColumnMappings)
            {
                var value = mapping.Property.PropertyInfo?.GetReflector().GetValue(item);
                if (value == null)
                {
                    writer.WriteNull();
                }
                else
                {
                    writer.Write(value);
                }
            }
        }
    }

    private async Task WriteRowsAsync(NpgsqlBinaryImporter writer, CancellationToken cancellationToken)
    {
        foreach (var item in Items)
        {
            await writer.StartRowAsync(cancellationToken);
            foreach (var mapping in ColumnMappings)
            {
                var value = mapping.Property.PropertyInfo?.GetReflector().GetValue(item);
                if (value == null)
                {
                    await writer.WriteNullAsync(cancellationToken);
                }
                else
                {
                    await writer.WriteAsync(value, cancellationToken);
                }
            }
        }
    }

    private string BuildTempTableSql()
    {
        var cols = ColumnMappings.Select(m => $"{QuoteIdentifier(m.Column.Name)} {m.Column.StoreType}");
        return $"CREATE TEMP TABLE {TmpTable} ({string.Join(", ", cols)}) ON COMMIT DROP;";
    }

    private string BuildDeleteSql(IList<string> updateOn, BulkMatchOptions<T> options)
    {
        // DELETE ... USING temp：等价于「目标表 JOIN 临时表后删除匹配行」。
        var conditions = string.Join(" AND ", updateOn.Select(c =>
            $"{QuoteIdentifier(options.TargetAlias)}.{QuoteIdentifier(c)} = {QuoteIdentifier(options.SourceAlias)}.{QuoteIdentifier(c)}"));
        return $"DELETE FROM {GetQuotedTableName()} AS {QuoteIdentifier(options.TargetAlias)} USING {TmpTable} AS {QuoteIdentifier(options.SourceAlias)} WHERE {conditions};";
    }

    private string BuildUpdateSql(IList<string> updateOn, BulkMatchOptions<T> options)
    {
        // UPDATE ... FROM temp：匹配列作 JOIN 条件，其余列从源表赋值（匹配列本身不更新）。
        var matchSet = updateOn.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sets = ColumnMappings
            .Where(m => !matchSet.Contains(m.Column.Name))
            .Select(m => $"{QuoteIdentifier(m.Column.Name)} = {QuoteIdentifier(options.SourceAlias)}.{QuoteIdentifier(m.Column.Name)}");
        var conditions = string.Join(" AND ", updateOn.Select(c =>
            $"{QuoteIdentifier(options.TargetAlias)}.{QuoteIdentifier(c)} = {QuoteIdentifier(options.SourceAlias)}.{QuoteIdentifier(c)}"));
        return $"UPDATE {GetQuotedTableName()} AS {QuoteIdentifier(options.TargetAlias)} SET {string.Join(", ", sets)} FROM {TmpTable} AS {QuoteIdentifier(options.SourceAlias)} WHERE {conditions};";
    }

    private string BuildMergeSql(IList<string> updateOn, BulkMatchOptions<T> options)
    {
        // PG 无 MERGE（旧版本）时用标准 Upsert：INSERT 全量 + ON CONFLICT(匹配列) DO UPDATE。
        // EXCLUDED 代表「本将插入但发生冲突的那一行」。
        var matchSet = updateOn.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var insertCols = string.Join(", ", ColumnMappings.Select(m => QuoteIdentifier(m.Column.Name)));
        var selectCols = string.Join(", ", ColumnMappings.Select(m => $"{QuoteIdentifier(options.SourceAlias)}.{QuoteIdentifier(m.Column.Name)}"));
        var conflict = string.Join(", ", updateOn.Select(QuoteIdentifier));
        var updates = ColumnMappings
            .Where(m => !matchSet.Contains(m.Column.Name))
            .Select(m => $"{QuoteIdentifier(m.Column.Name)} = EXCLUDED.{QuoteIdentifier(m.Column.Name)}");
        var updateClause = updates.Any()
            ? $"DO UPDATE SET {string.Join(", ", updates)}"
            : "DO NOTHING";

        return new StringBuilder()
            .Append($"INSERT INTO {GetQuotedTableName()} ({insertCols}) ")
            .Append($"SELECT {selectCols} FROM {TmpTable} AS {QuoteIdentifier(options.SourceAlias)} ")
            .Append($"ON CONFLICT ({conflict}) {updateClause};")
            .ToString();
    }

    private static (NpgsqlConnection, NpgsqlTransaction) Cast(DbConnection connection, DbTransaction transaction)
    {
        if (connection is not NpgsqlConnection npgsqlConn)
        {
            throw new NotSupportedException("This operation only supports PostgreSQL.");
        }

        if (transaction is not NpgsqlTransaction npgsqlTrans)
        {
            throw new NotSupportedException("This operation only supports PostgreSQL transactions.");
        }

        return (npgsqlConn, npgsqlTrans);
    }
}

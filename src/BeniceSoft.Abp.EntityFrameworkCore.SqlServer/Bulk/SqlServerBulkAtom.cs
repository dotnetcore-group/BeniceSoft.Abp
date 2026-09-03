using BeniceSoft.Abp.EntityFrameworkCore.Bulk;
using BeniceSoft.Core;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore.SqlServer.Bulk;

/// <summary>
/// SQL Server 批量写实现。
/// <para>
/// <b>Insert：</b><c>SqlBulkCopy</c> 直写目标表（服务端 TDS 批量协议，远快于逐行 INSERT）<br/>
/// <b>Update / Delete / Merge：</b>
/// ① 建会话临时表 <c>#TmpTable</c> 
/// ② BulkCopy 灌入源数据 
/// ③ <c>MERGE ... WITH (HOLDLOCK)</c> 按匹配列对齐目标表 
/// ④ DROP 临时表。
/// HOLDLOCK 降低并发下 MERGE 的幻读/竞态风险。
/// </para>
/// </summary>
public class SqlServerBulkAtom<T> : EfCoreBulkAtom<T>
    where T : class
{
    /// <summary>会话级临时表，仅当前连接可见，事务结束后自动清理；用于承载源数据再 MERGE</summary>
    private const string TmpTable = "#TmpTable";

    /// <summary>写入前后禁用全部非聚集索引（大表批量写时可减少索引维护开销，结束后 Rebuild）</summary>
    public bool DisableIndex { get; set; }

    /// <summary>仅禁用指定名称的非聚集索引（与 <see cref="DisableIndex"/> 组合使用）</summary>
    public ICollection<string> DisableIndexes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public SqlBulkCopyOptions BulkCopyOptions { get; set; } = SqlBulkCopyOptions.Default;

    public SqlServerBulkAtom(DbContext ctx, IEnumerable<T> items)
        : base(ctx, items)
    {
    }

    public new SqlServerBulkAtom<T> WithCommandTimeout(int seconds)
    {
        base.WithCommandTimeout(seconds);
        return this;
    }

    public new SqlServerBulkAtom<T> WithBulkCopyTimeout(int seconds)
    {
        base.WithBulkCopyTimeout(seconds);
        return this;
    }

    public new SqlServerBulkAtom<T> WithBulkCopyEnableStreaming(bool status)
    {
        base.WithBulkCopyEnableStreaming(status);
        return this;
    }

    public new SqlServerBulkAtom<T> WithBulkCopyNotifyAfter(int rows)
    {
        base.WithBulkCopyNotifyAfter(rows);
        return this;
    }

    public new SqlServerBulkAtom<T> WithBulkCopyBatchSize(int rows)
    {
        base.WithBulkCopyBatchSize(rows);
        return this;
    }

    public new SqlServerBulkAtom<T> RemoveColumn(System.Linq.Expressions.Expression<Func<T, object>> keyExpression)
    {
        base.RemoveColumn(keyExpression);
        return this;
    }

    public SqlServerBulkAtom<T> WithBulkCopyOptions(SqlBulkCopyOptions options)
    {
        BulkCopyOptions = options;
        return this;
    }

    public SqlServerBulkAtom<T> AddDisableNonClusteredIndex(string indexName)
    {
        DisableIndexes.Add(indexName);
        return this;
    }

    public SqlServerBulkAtom<T> DisableAllNonClusteredIndexes()
    {
        DisableIndex = true;
        return this;
    }

    protected override string QuoteIdentifier(string name) => $"[{name}]";

    /// <summary>
    /// 直写目标表：可选地先 Disable 非聚集索引 → SqlBulkCopy.WriteToServer → Rebuild 索引
    /// </summary>
    protected override int BulkInsert(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction)
    {
        var (sqlConn, sqlTrans) = Cast(connection, transaction);
        using var command = sqlConn.CreateCommand();
        command.Transaction = sqlTrans;
        command.CommandTimeout = Options.CommandTimeout;

        if (DisableIndex)
        {
            command.CommandText = BuildIndex("Disable");
            command.ExecuteNonQuery();
        }

        // SqlBulkCopy 按列名映射把 DataTable 批量推入目标表，绕过逐条参数化 INSERT
        CreateBulkCopy(sqlConn, sqlTrans).WriteToServer(BuildDataTable());

        if (DisableIndex)
        {
            command.CommandText = BuildIndex("Rebuild");
            command.ExecuteNonQuery();
        }

        return Items.Count();
    }

    protected override async Task<int> BulkInsertAsync(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction, CancellationToken cancellationToken)
    {
        var (sqlConn, sqlTrans) = Cast(connection, transaction);
        await using var command = sqlConn.CreateCommand();
        command.Transaction = sqlTrans;
        command.CommandTimeout = Options.CommandTimeout;

        if (DisableIndex)
        {
            command.CommandText = BuildIndex("Disable");
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await CreateBulkCopy(sqlConn, sqlTrans).WriteToServerAsync(BuildDataTable(), cancellationToken);

        if (DisableIndex)
        {
            command.CommandText = BuildIndex("Rebuild");
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return Items.Count();
    }

    protected override int BulkDelete(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction, Action<BulkMatchOptions<T>>? setup)
        => ExecuteMergePath(connection, transaction, setup, BuildDeleteText);

    protected override Task<int> BulkDeleteAsync(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction, Action<BulkMatchOptions<T>>? setup, CancellationToken cancellationToken)
        => ExecuteMergePathAsync(connection, transaction, setup, BuildDeleteText, cancellationToken);

    protected override int BulkUpdate(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction, Action<BulkMatchOptions<T>>? setup)
        => ExecuteMergePath(connection, transaction, setup, BuildUpdateText);

    protected override Task<int> BulkUpdateAsync(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction, Action<BulkMatchOptions<T>>? setup, CancellationToken cancellationToken)
        => ExecuteMergePathAsync(connection, transaction, setup, BuildUpdateText, cancellationToken);

    protected override int BulkMerge(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction, Action<BulkMatchOptions<T>>? setup)
        => ExecuteMergePath(connection, transaction, setup, BuildMergeText);

    protected override Task<int> BulkMergeAsync(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction, Action<BulkMatchOptions<T>>? setup, CancellationToken cancellationToken)
        => ExecuteMergePathAsync(connection, transaction, setup, BuildMergeText, cancellationToken);

    /// <summary>
    /// Update/Delete/Merge 共用路径：临时表承接源数据，再用 MERGE 一次对齐目标表
    /// 避免「逐行 UPDATE」或「大 IN 列表」
    /// </summary>
    private int ExecuteMergePath(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        Action<BulkMatchOptions<T>>? setup,
        Func<IList<string>, BulkMatchOptions<T>, string> buildSql)
    {
        var (sqlConn, sqlTrans) = Cast(connection, transaction);
        var match = new BulkMatchOptions<T>(this);
        setup?.Invoke(match);
        var updateOn = match.GetMatchColumns();

        using var command = sqlConn.CreateCommand();
        command.Transaction = sqlTrans;
        command.CommandTimeout = Options.CommandTimeout;

        // ① 建与目标表同结构的 #TmpTable，作为 MERGE 的 USING 源
        command.CommandText = BuildTmpTable();
        command.ExecuteNonQuery();

        // ② 把内存数据 bulk 进临时表（仍走 SqlBulkCopy，吞吐高）
        var bulkCopy = CreateBulkCopy(sqlConn, sqlTrans);
        bulkCopy.DestinationTableName = TmpTable;
        bulkCopy.WriteToServer(BuildDataTable());

        if (DisableIndex)
        {
            command.CommandText = BuildIndex("Disable");
            command.ExecuteNonQuery();
        }

        // ③ MERGE：按匹配列 WHEN MATCHED / NOT MATCHED 执行删、改或 upsert；语句末尾 DROP 临时表
        command.CommandText = buildSql(updateOn, match);
        var result = command.ExecuteNonQuery();

        if (DisableIndex)
        {
            command.CommandText = BuildIndex("Rebuild");
            command.ExecuteNonQuery();
        }

        return result;
    }

    private async Task<int> ExecuteMergePathAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        Action<BulkMatchOptions<T>>? setup,
        Func<IList<string>, BulkMatchOptions<T>, string> buildSql,
        CancellationToken cancellationToken)
    {
        var (sqlConn, sqlTrans) = Cast(connection, transaction);
        var match = new BulkMatchOptions<T>(this);
        setup?.Invoke(match);
        var updateOn = match.GetMatchColumns();

        await using var command = sqlConn.CreateCommand();
        command.Transaction = sqlTrans;
        command.CommandTimeout = Options.CommandTimeout;
        command.CommandText = BuildTmpTable();
        await command.ExecuteNonQueryAsync(cancellationToken);

        var bulkCopy = CreateBulkCopy(sqlConn, sqlTrans);
        bulkCopy.DestinationTableName = TmpTable;
        await bulkCopy.WriteToServerAsync(BuildDataTable(), cancellationToken);

        if (DisableIndex)
        {
            command.CommandText = BuildIndex("Disable");
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        command.CommandText = buildSql(updateOn, match);
        var result = await command.ExecuteNonQueryAsync(cancellationToken);

        if (DisableIndex)
        {
            command.CommandText = BuildIndex("Rebuild");
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return result;
    }

    private static (SqlConnection, SqlTransaction) Cast(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction)
    {
        if (connection is not SqlConnection sqlConn)
        {
            throw new NotSupportedException("This operation only supports SQL Server.");
        }

        if (transaction is not SqlTransaction sqlTrans)
        {
            throw new NotSupportedException("This operation only supports SQL Server transactions.");
        }

        return (sqlConn, sqlTrans);
    }

    private string BuildTmpTable()
    {
        var cols = ColumnMappings.Select(m => $"[{m.Column.Name}] {m.Column.StoreType}");
        return $"CREATE TABLE {TmpTable}({string.Join(", ", cols)});";
    }

    private string BuildIndex(string action)
    {
        var filter = DisableIndexes.IsNotNull()
            ? string.Concat(DisableIndexes.Select(index => $" AND sys.indexes.name = '{index}'"))
            : string.Empty;

        return $"DECLARE @sql AS VARCHAR(MAX)=''; SELECT @sql+='ALTER INDEX [' + sys.indexes.name + '] ON [' + sys.objects.name + '] {action};'FROM sys.indexes JOIN sys.objects ON sys.indexes.object_id = sys.objects.object_id WHERE sys.indexes.type_desc = 'NONCLUSTERED' AND sys.objects.type_desc = 'USER_TABLE' AND sys.objects.name = '{Options.TableName}'{filter};EXEC(@sql)";
    }

    /// <summary>
    /// 配置 SqlBulkCopy：目标表、超时、批大小、列映射。列按「同名」映射到目标/临时表，与 BuildDataTable 列名一致
    /// </summary>
    private SqlBulkCopy CreateBulkCopy(SqlConnection connection, SqlTransaction transaction)
    {
        var bulkCopy = new SqlBulkCopy(connection, BulkCopyOptions, transaction)
        {
            DestinationTableName = GetQuotedTableName(),
            EnableStreaming = Options.BulkCopyEnableStreaming,
            BulkCopyTimeout = Options.BulkCopyTimeout
        };

        if (Options.BulkCopyBatchSize.HasValue)
        {
            bulkCopy.BatchSize = Options.BulkCopyBatchSize.Value;
        }

        if (Options.BulkCopyNotifyAfter.HasValue)
        {
            bulkCopy.NotifyAfter = Options.BulkCopyNotifyAfter.Value;
        }

        foreach (var mapping in ColumnMappings)
        {
            bulkCopy.ColumnMappings.Add(mapping.Column.Name, mapping.Column.Name);
        }

        return bulkCopy;
    }

    private static string BuildConditions(IList<string> updateOn, string sourceAlias, string targetAlias)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"ON [{targetAlias}].[{updateOn[0]}] = [{sourceAlias}].[{updateOn[0]}] ");
        for (var i = 1; i < updateOn.Count; i++)
        {
            sb.Append($"AND [{targetAlias}].[{updateOn[i]}] = [{sourceAlias}].[{updateOn[i]}]");
        }

        return sb.ToString();
    }

    private string BuildUpdateSet(string sourceAlias, string targetAlias)
    {
        var sets = ColumnMappings.Select(m => $"[{targetAlias}].[{m.Column.Name}] = [{sourceAlias}].[{m.Column.Name}] ");
        return "UPDATE SET " + string.Join(", ", sets);
    }

    private string BuildInsertSet(string sourceAlias)
    {
        var cols = ColumnMappings.Select(m => $"[{m.Column.Name}]");
        var vals = ColumnMappings.Select(m => $"[{sourceAlias}].[{m.Column.Name}]");
        return $"INSERT ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)})";
    }

    private string BuildDeleteText(IList<string> updateOn, BulkMatchOptions<T> options)
        // 仅 WHEN MATCHED THEN DELETE：临时表里有的键，目标表对应行删除。
        => $"MERGE INTO {GetQuotedTableName()} WITH (HOLDLOCK) AS {options.TargetAlias} USING {TmpTable} AS {options.SourceAlias} {BuildConditions(updateOn, options.SourceAlias, options.TargetAlias)} WHEN MATCHED THEN DELETE; DROP TABLE {TmpTable};";

    private string BuildUpdateText(IList<string> updateOn, BulkMatchOptions<T> options)
        // 仅 WHEN MATCHED THEN UPDATE：用源行覆盖目标行各列。
        => $"MERGE INTO {GetQuotedTableName()} WITH (HOLDLOCK) AS {options.TargetAlias} USING {TmpTable} AS {options.SourceAlias} {BuildConditions(updateOn, options.SourceAlias, options.TargetAlias)} WHEN MATCHED THEN {BuildUpdateSet(options.SourceAlias, options.TargetAlias)}; DROP TABLE {TmpTable};";

    private string BuildMergeText(IList<string> updateOn, BulkMatchOptions<T> options)
        // Upsert：匹配则更新，目标侧不存在则插入。
        => $"MERGE INTO {GetQuotedTableName()} WITH (HOLDLOCK) AS {options.TargetAlias} USING {TmpTable} AS {options.SourceAlias} {BuildConditions(updateOn, options.SourceAlias, options.TargetAlias)} WHEN MATCHED THEN {BuildUpdateSet(options.SourceAlias, options.TargetAlias)} WHEN NOT MATCHED BY TARGET THEN {BuildInsertSet(options.SourceAlias)}; DROP TABLE {TmpTable};";
}

using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace BeniceSoft.Abp.EntityFrameworkCore;

/// <summary>
/// 包装真实连接：CreateCommand 时返回可劫持 DataReader 的 CreateEntityCommand，
/// 以便 Future 批执行后把当前结果集注入 EF 编译查询枚举器。
/// </summary>
internal sealed class CreateEntityConnection(DbConnection originalConnection, DbDataReader? originalDataReader) : DbConnection
{
    private DbConnection OriginalConnection { get; } = originalConnection;

    internal DbDataReader? OriginalDataReader { get; set; } = originalDataReader;

    [AllowNull]
    public override string ConnectionString
    {
        get => OriginalConnection.ConnectionString;
        set => OriginalConnection.ConnectionString = value!;
    }

    public override string Database => OriginalConnection.Database;

    public override string DataSource => OriginalConnection.DataSource;

    public override string ServerVersion => OriginalConnection.ServerVersion;

    public override ConnectionState State => OriginalConnection.State;

    public override void ChangeDatabase(string databaseName)
        => OriginalConnection.ChangeDatabase(databaseName);

    public override void Close()
        => OriginalConnection.Close();

    public override void Open()
        => OriginalConnection.Open();

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        => OriginalConnection.BeginTransaction(isolationLevel);

    protected override DbCommand CreateDbCommand()
        => new CreateEntityCommand(OriginalConnection.CreateCommand(), OriginalDataReader);
}

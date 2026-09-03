using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace BeniceSoft.Abp.EntityFrameworkCore;

/// <summary>
/// 若已注入批查询的 DataReader 则直接返回，否则回落到原始 ExecuteReader。
/// </summary>
internal sealed class CreateEntityCommand(DbCommand originalCommand, DbDataReader? originalDataReader) : DbCommand
{
    [AllowNull]
    public override string CommandText
    {
        get => originalCommand.CommandText;
        set => originalCommand.CommandText = value!;
    }

    public override int CommandTimeout
    {
        get => originalCommand.CommandTimeout;
        set => originalCommand.CommandTimeout = value;
    }

    public override CommandType CommandType
    {
        get => originalCommand.CommandType;
        set => originalCommand.CommandType = value;
    }

    public override bool DesignTimeVisible
    {
        get => originalCommand.DesignTimeVisible;
        set => originalCommand.DesignTimeVisible = value;
    }

    public override UpdateRowSource UpdatedRowSource
    {
        get => originalCommand.UpdatedRowSource;
        set => originalCommand.UpdatedRowSource = value;
    }

    protected override DbConnection? DbConnection
    {
        get => originalCommand.Connection;
        set => originalCommand.Connection = value;
    }

    protected override DbParameterCollection DbParameterCollection => originalCommand.Parameters;

    protected override DbTransaction? DbTransaction
    {
        get => originalCommand.Transaction;
        set => originalCommand.Transaction = value;
    }

    public override void Cancel() => originalCommand.Cancel();

    public override int ExecuteNonQuery() => originalCommand.ExecuteNonQuery();

    public override object? ExecuteScalar() => originalCommand.ExecuteScalar();

    public override void Prepare() => originalCommand.Prepare();

    protected override DbParameter CreateDbParameter() => originalCommand.CreateParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        => originalDataReader == null || originalDataReader.IsClosed
            ? originalCommand.ExecuteReader(behavior)
            : originalDataReader;
}

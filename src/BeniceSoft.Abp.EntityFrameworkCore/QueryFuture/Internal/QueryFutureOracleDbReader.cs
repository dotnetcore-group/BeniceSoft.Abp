using System.Collections;
using System.Data;
using System.Data.Common;

namespace BeniceSoft.Abp.EntityFrameworkCore;

/// <summary>Oracle 结果集包装：将 16 字节 RAW 读为 Guid。</summary>
internal sealed class QueryFutureOracleDbReader(DbDataReader reader) : DbDataReader
{
    public DbDataReader Reader { get; } = reader;

    public override int Depth => Reader.Depth;

    public override bool IsClosed => Reader.IsClosed;

    public override int RecordsAffected => Reader.RecordsAffected;

    public override int FieldCount => Reader.FieldCount;

    public override object this[int ordinal] => Reader[ordinal];

    public override object this[string name] => Reader[name];

    public override bool HasRows => Reader.HasRows;

    public override void Close() => Reader.Close();

    public override DataTable? GetSchemaTable() => Reader.GetSchemaTable();

    public override bool NextResult() => Reader.NextResult();

    public override bool Read() => Reader.Read();

    public override bool GetBoolean(int ordinal) => Reader.GetBoolean(ordinal);

    public override byte GetByte(int ordinal) => Reader.GetByte(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        => Reader.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);

    public override char GetChar(int ordinal) => Reader.GetChar(ordinal);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        => Reader.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);

    public override Guid GetGuid(int ordinal)
    {
        var value = Reader.GetValue(ordinal);
        return new Guid((byte[])value);
    }

    public override short GetInt16(int ordinal) => Reader.GetInt16(ordinal);

    public override int GetInt32(int ordinal) => Reader.GetInt32(ordinal);

    public override long GetInt64(int ordinal) => Reader.GetInt64(ordinal);

    public override DateTime GetDateTime(int ordinal) => Reader.GetDateTime(ordinal);

    public override string GetString(int ordinal) => Reader.GetString(ordinal);

    public override decimal GetDecimal(int ordinal) => Reader.GetDecimal(ordinal);

    public override double GetDouble(int ordinal) => Reader.GetDouble(ordinal);

    public override float GetFloat(int ordinal) => Reader.GetFloat(ordinal);

    public override string GetName(int ordinal) => Reader.GetName(ordinal);

    public override int GetValues(object[] values) => Reader.GetValues(values);

    public override bool IsDBNull(int ordinal) => Reader.IsDBNull(ordinal);

    public override int GetOrdinal(string name) => Reader.GetOrdinal(name);

    public override string GetDataTypeName(int ordinal) => Reader.GetDataTypeName(ordinal);

    public override Type GetFieldType(int ordinal) => Reader.GetFieldType(ordinal);

    public override object GetValue(int ordinal)
    {
        var value = Reader.GetValue(ordinal);
        if (value.GetType() == typeof(byte[]) && ((byte[])value).Length == 16)
        {
            return new Guid((byte[])value);
        }

        return value;
    }

    public override IEnumerator GetEnumerator() => Reader.GetEnumerator();
}

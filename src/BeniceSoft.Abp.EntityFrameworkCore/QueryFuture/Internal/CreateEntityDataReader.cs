using System.Collections;
using System.Data.Common;

namespace BeniceSoft.Abp.EntityFrameworkCore;

/// <summary>对批查询 DataReader 的薄包装，兼容 DateTimeOffset / Guid(byte[]) 字段读取。</summary>
internal sealed class CreateEntityDataReader(DbDataReader originalDataReader) : DbDataReader
{
    public override object this[string name] => originalDataReader[name];

    public override object this[int ordinal] => originalDataReader[ordinal];

    public override int Depth => originalDataReader.Depth;

    public override int FieldCount => originalDataReader.FieldCount;

    public override bool HasRows => originalDataReader.HasRows;

    public override bool IsClosed => originalDataReader.IsClosed;

    public override int RecordsAffected => originalDataReader.RecordsAffected;

    public override bool GetBoolean(int ordinal) => originalDataReader.GetBoolean(ordinal);

    public override byte GetByte(int ordinal) => originalDataReader.GetByte(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        => originalDataReader.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);

    public override char GetChar(int ordinal) => originalDataReader.GetChar(ordinal);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        => originalDataReader.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);

    public override string GetDataTypeName(int ordinal) => originalDataReader.GetDataTypeName(ordinal);

    public override DateTime GetDateTime(int ordinal) => originalDataReader.GetDateTime(ordinal);

    public override decimal GetDecimal(int ordinal) => originalDataReader.GetDecimal(ordinal);

    public override double GetDouble(int ordinal) => originalDataReader.GetDouble(ordinal);

    public override IEnumerator GetEnumerator() => originalDataReader.GetEnumerator();

    public override Type GetFieldType(int ordinal) => originalDataReader.GetFieldType(ordinal);

    public override float GetFloat(int ordinal) => originalDataReader.GetFloat(ordinal);

    public override Guid GetGuid(int ordinal) => originalDataReader.GetGuid(ordinal);

    public override short GetInt16(int ordinal) => originalDataReader.GetInt16(ordinal);

    public override int GetInt32(int ordinal) => originalDataReader.GetInt32(ordinal);

    public override long GetInt64(int ordinal) => originalDataReader.GetInt64(ordinal);

    public override string GetName(int ordinal) => originalDataReader.GetName(ordinal);

    public override int GetOrdinal(string name) => originalDataReader.GetOrdinal(name);

    public override string GetString(int ordinal) => originalDataReader.GetString(ordinal);

    public override object GetValue(int ordinal) => originalDataReader.GetValue(ordinal);

    public override int GetValues(object[] values) => originalDataReader.GetValues(values);

    public override bool IsDBNull(int ordinal) => originalDataReader.IsDBNull(ordinal);

    public override bool NextResult() => originalDataReader.NextResult();

    public override bool Read() => originalDataReader.Read();

    public override T GetFieldValue<T>(int ordinal)
    {
        var value = GetValue(ordinal);

        if (typeof(T) == typeof(DateTimeOffset) && value is DateTime valueDateTime)
        {
            value = new DateTimeOffset(valueDateTime);
        }
        else if (typeof(T) == typeof(Guid) && value is byte[] valueByteArray && valueByteArray.Length == 16)
        {
            value = new Guid(valueByteArray);
        }

        return (T)value;
    }
}

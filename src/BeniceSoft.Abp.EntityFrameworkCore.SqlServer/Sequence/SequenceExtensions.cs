using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Numerics;

namespace BeniceSoft.Abp.EntityFrameworkCore.SqlServer;

public static class SequenceExtensions
{
    public static T GetSequence<T>(this DatabaseFacade databaseFacade, string name)
        where T : INumber<T>
        => databaseFacade.GetSequence<T>(name, 1).Single();

    public static T[] GetSequence<T>(this DatabaseFacade databaseFacade, string name, int count)
        where T : INumber<T>
        => [.. databaseFacade.SqlQueryRaw<T>(GetSql<T>(name, count))];

    public static async Task<T> GetSequenceAsync<T>(this DatabaseFacade databaseFacade, string name, CancellationToken cancellationToken = default)
        where T : INumber<T>
    {
        var array = await databaseFacade.GetSequenceAsync<T>(name, 1, cancellationToken);
        return array.Single();
    }

    public static Task<T[]> GetSequenceAsync<T>(this DatabaseFacade databaseFacade, string name, int count, CancellationToken cancellationToken = default)
        where T : INumber<T>
        => databaseFacade.SqlQueryRaw<T>(GetSql<T>(name, count)).ToArrayAsync(cancellationToken);

    private static string GetSql<T>(string name, int count)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        if (count == 1)
        {
            return $"SELECT NEXT VALUE FOR [{name}] AS Seq";
        }

        var dbType = Type.GetTypeCode(typeof(T)) switch
        {
            TypeCode.Byte => "TINYINT",
            TypeCode.Int16 => "SMALLINT",
            TypeCode.Int32 => "INT",
            TypeCode.Int64 => "BIGINT",
            TypeCode.Decimal => "DECIMAL",
            _ => throw new NotSupportedException(typeof(T).Name)
        };

        return $"DECLARE @count INT;CREATE TABLE #TmpSeq (Seq {dbType});SET @count={count};WHILE @count>0 BEGIN INSERT INTO #TmpSeq(Seq) VALUES(NEXT VALUE FOR [{name}]);SET @count=@count-1;END SELECT Seq FROM #TmpSeq; DROP TABLE #TmpSeq";
    }
}

using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BeniceSoft.Abp.EntityFrameworkCore.PostgreSql;

public static class SequenceExtensions
{
    public static T GetSequence<T>(this DatabaseFacade databaseFacade, string name)
        where T : INumber<T>
        => databaseFacade.GetSequence<T>(name, 1).Single();

    public static T[] GetSequence<T>(this DatabaseFacade databaseFacade, string name, int count)
        where T : INumber<T>
        => [.. databaseFacade.SqlQueryRaw<T>(GetSql(name, count))];

    public static async Task<T> GetSequenceAsync<T>(this DatabaseFacade databaseFacade, string name, CancellationToken cancellationToken = default)
        where T : INumber<T>
    {
        var array = await databaseFacade.GetSequenceAsync<T>(name, 1, cancellationToken);
        return array.Single();
    }

    public static Task<T[]> GetSequenceAsync<T>(this DatabaseFacade databaseFacade, string name, int count, CancellationToken cancellationToken = default)
        where T : INumber<T>
        => databaseFacade.SqlQueryRaw<T>(GetSql(name, count)).ToArrayAsync(cancellationToken);

    private static string GetSql(string name, int count)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        return $"SELECT NEXTVAL('\"{name}\"') AS Seq FROM generate_series(1,{count})";
    }
}

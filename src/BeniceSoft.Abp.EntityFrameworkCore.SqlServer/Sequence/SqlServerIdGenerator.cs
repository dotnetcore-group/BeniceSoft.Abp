using BeniceSoft.Core.Strategy;
using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore.SqlServer;

/// <summary>
/// <see cref="IIdGenerator"/> backed by a SQL Server SEQUENCE.
/// </summary>
public class SqlServerIdGenerator : IIdGenerator
{
    private readonly DbContext _context;
    private readonly string _name;

    public SqlServerIdGenerator(DbContext context, string name)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(name);
        _context = context;
        _name = name;
    }

    public long NewSequenceId() => _context.Database.GetSequence<long>(_name);

    public long[] NewSequenceId(int count) => _context.Database.GetSequence<long>(_name, count);

    public Task<long> NewSequenceIdAsync() => _context.Database.GetSequenceAsync<long>(_name);

    public Task<long[]> NewSequenceIdAsync(int count) => _context.Database.GetSequenceAsync<long>(_name, count);
}

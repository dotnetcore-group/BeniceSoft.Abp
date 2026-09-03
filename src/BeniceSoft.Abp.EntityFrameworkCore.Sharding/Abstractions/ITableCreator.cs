using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface ITableCreator
{
    void Create<T>(string dataSource, string tail)
        where T : class;

    void Create(string dataSource, Type entityType, string tail);
}

internal sealed class TableCreator(IShardingProvider shardingProvider, ShardingOptions options, IRouteTailFactory routeTailFactory, IDbContextCreator dbContextCreator,
    ILogger<TableCreator> logger) : ITableCreator
{
    public void Create<T>(string dataSource, string tail)
        where T : class
    {
        Create(dataSource, typeof(T), tail);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dataSource"></param>
    /// <param name="entityType"></param>
    /// <param name="tail"></param>
    public void Create(string dataSource, Type entityType,
        string tail)
    {
        using var scope = shardingProvider.CreateScope();

        using var context = dbContextCreator.GetShell(scope);

        using var ctx = ((IShardingDbContext)context).GetWriteDbContext(dataSource, routeTailFactory.Create(tail, false));

        ctx.RemoveShardingModel(entityType);
        var databaseCreator = ctx.Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator
            ?? throw new InvalidOperationException("Unable to resolve RelationalDatabaseCreator.");

        try
        {
            databaseCreator.CreateTables();
        }
        catch (Exception ex)
        {
            if (!options.IgnoreCreateTableError)
            {
                logger.LogWarning(ex, "create table error entity name:[{Name}].", entityType.Name);
                throw new ShardingException($" create table error :{ex.Message}", ex);
            }
        }
    }
}

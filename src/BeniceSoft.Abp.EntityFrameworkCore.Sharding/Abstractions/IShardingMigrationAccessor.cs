using BeniceSoft.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IShardingMigrationAccessor
{
    ShardingMigrationContext? Context { get; set; }
}

internal sealed class ShardingMigrationAccessor : IShardingMigrationAccessor
{
    private static readonly AsyncLocal<ShardingMigrationContext?> _local = new();

    public ShardingMigrationContext? Context
    {
        get => _local.Value;
        set => _local.Value = value;
    }
}

public sealed class ShardingMigrationContext
{
    /// <summary>
    /// 当前的数据源名称
    /// </summary>
    public string DataSource { get; set; } = string.Empty;
}

public sealed class ShardingMigrationScope(IShardingMigrationAccessor accessor, ShardingMigrationContext? previous) : IDisposable
{
    public void Dispose()
    {
        accessor.Context = previous;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 迁移执行单元
/// </summary>
internal sealed class MigrateUnit(DbContext shellDbContext, string dataSource)
{

    /// <summary>
    /// 壳dbcontext
    /// </summary>
    public DbContext ShellDbContext { get; } = shellDbContext;

    /// <summary>
    /// 数据源名称
    /// </summary>
    public string DataSource { get; } = dataSource;
}

internal sealed class ShardingMigrationScriptGenerator(IShardingRuntimeContext context, string? fromMigration, string? toMigration, MigrationsSqlGenerationOptions options)
{
    private string GenerateScriptSql(IMigrator migrator)
    {
        return migrator.GenerateScript(fromMigration, toMigration, options);
    }

    private string ExecuteMigrateUnits(IShardingRuntimeContext context, List<MigrateUnit> migrateUnits)
    {
        var manager = context.MigrationManager;
        var dbContextCreator = context.RouteTailDbContextCreator;
        var routeTailFactory = context.RouteTailFactory;

        var migrateTasks = migrateUnits.Select(migrateUnit =>
        {
            return Task.Run(() =>
            {
                using (manager.CreateScope())
                {
                    manager.Current!.DataSource = migrateUnit.DataSource;
                    var dbContextOptions = context.CreateShellDbContextOptions(migrateUnit.DataSource);

                    using var ctx = dbContextCreator.Create(migrateUnit.ShellDbContext, new ShardingDbContextOptions(dbContextOptions, routeTailFactory.Create(string.Empty, false)));
                    var migrator = ctx.GetService<IMigrator>();
                    return $"-- DataSource:{migrateUnit.DataSource}" + Environment.NewLine + GenerateScriptSql(migrator) + Environment.NewLine;
                }
            });
        }).ToArray();

        var scripts = TaskHelper.WhenAllFastFail(migrateTasks).GetResult();
        return scripts.JoinStr(Environment.NewLine);
    }

    public string GenerateScript()
    {
        var allDataSource = context.VirtualDataSource.GetAllDataSource();
        var defaultDataSource = context.VirtualDataSource.DefaultDataSource;

        using var scope = context.ShardingProvider.CreateScope();
        using var shellDbContext = context.DbContextCreator.GetShell(scope);
        var parallelCount = context.Options.MigrationParallelCount;
        if (parallelCount <= 0)
        {
            throw new ShardingInvalidOperationException($"migration parallel count must >0");
        }

        //默认数据源需要最后执行 否则可能会导致异常的情况下GetPendingMigrations为空
        var partitionUnits = allDataSource.Where(o => o != defaultDataSource).Partition(parallelCount);
        var sb = new StringBuilder();
        foreach (var units in partitionUnits)
        {
            var migrateUnits = units.Select(o => new MigrateUnit(shellDbContext, o)).ToList();
            var scriptSql = ExecuteMigrateUnits(context, migrateUnits);
            sb.AppendLine(scriptSql);
        }

        //包含默认默认的单独最后一次处理
        if (allDataSource.Contains(defaultDataSource))
        {
            var scriptSql = ExecuteMigrateUnits(context, [new(shellDbContext, defaultDataSource)]);
            sb.AppendLine(scriptSql);
        }

        return sb.ToString();
    }
}

public interface IShardingMigrationManager
{
    ShardingMigrationContext? Current { get; }

    /// <summary>
    /// 创建路由scope
    /// </summary>
    /// <returns></returns>
    ShardingMigrationScope CreateScope();
}

internal sealed class ShardingMigrationManager(IShardingMigrationAccessor accessor) : IShardingMigrationManager
{
    public ShardingMigrationContext? Current => accessor.Context;

    public ShardingMigrationScope CreateScope()
    {
        var previous = accessor.Context;
        accessor.Context = new();
        return new ShardingMigrationScope(accessor, previous);
    }
}

using BeniceSoft.Core;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface ITableEnsureManager
{
    ISet<string> GetTables(IShardingDbContext context, string dataSource);
}

internal abstract class TableEnsureManager(IRouteTailFactory routeTailFactory) : ITableEnsureManager
{
    protected IRouteTailFactory RouteTailFactory { get; } = routeTailFactory;

    public ISet<string> GetTables(IShardingDbContext context, string dataSource)
    {
        using var ctx = context.GetWriteDbContext(dataSource, RouteTailFactory.Create(string.Empty));
        var conn = ctx.Database.GetDbConnection();
        conn.Open();
        return GetTables(conn, dataSource);
    }

    public abstract ISet<string> GetTables(DbConnection connection, string dataSource);
}

internal sealed class ConventionTableEnsureManager(IRouteTailFactory routeTailFactory) : TableEnsureManager(routeTailFactory)
{
    public override ISet<string> GetTables(DbConnection connection, string dataSource)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var dt = connection.GetSchema("Tables");

        foreach (DataRow row in dt.Rows)
        {
            var schema = row["TABLE_NAME"].ToStringSafe();
            result.Add(schema);
        }

        return result;
    }
}

internal sealed class MySqlTableEnsureManager(IRouteTailFactory routeTailFactory) : TableEnsureManager(routeTailFactory)
{
    public override ISet<string> GetTables(DbConnection connection, string dataSource)
    {
        var database = connection.Database;
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var dt = connection.GetSchema("Tables");

        foreach (DataRow row in dt.Rows)
        {
            var schema = row["TABLE_SCHEMA"].ToStringSafe();
            if (database.EqualsTo(schema, StringComparison.OrdinalIgnoreCase))
            {
                var tableName = row["TABLE_NAME"];
                result.Add($"{tableName}");
            }
        }

        return result;
    }
}

internal sealed class SqliteTableEnsureManager(IRouteTailFactory routeTailFactory) : TableEnsureManager(routeTailFactory)
{
    public override ISet<string> GetTables(DbConnection connection, string dataSource)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT tbl_name FROM sqlite_master;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var str = (string)reader["tbl_name"];
            result.Add(str);
        }

        return result;
    }
}

internal sealed class GuessTableEnsureManager(IRouteTailFactory routeTailFactory) : TableEnsureManager(routeTailFactory)
{
    private ConventionTableEnsureManager? _convention;
    private MySqlTableEnsureManager? _mySql;
    private SqliteTableEnsureManager? _sqlite;

    public override ISet<string> GetTables(DbConnection connection, string dataSource)
    {
        var fullName = connection.GetType().FullName;
        switch (fullName)
        {
            case "Microsoft.Data.SqlClient.SqlConnection":
            case "System.Data.SqlClient.SqlConnection":
            case "Oracle.ManagedDataAccess.Client.OracleConnection":
            case "Npgsql.NpgsqlConnection":
                {
                    _convention ??= new ConventionTableEnsureManager(RouteTailFactory);
                    return _convention.GetTables(connection, dataSource);
                }

            case "MySqlConnector.MySqlConnection":
            case "MySql.Data.MySqlClient.MySqlConnection":
                {
                    _mySql ??= new MySqlTableEnsureManager(RouteTailFactory);
                    return _mySql.GetTables(connection, dataSource);
                }

            case "Microsoft.Data.Sqlite.SqliteConnection":
            case "System.Data.Sqlite.SqliteConnection":
                {
                    _sqlite = new SqliteTableEnsureManager(RouteTailFactory);
                    return _sqlite.GetTables(connection, dataSource);
                }

            default:
                throw new ShardingNotSupportException($"{fullName}");

        }
    }
}

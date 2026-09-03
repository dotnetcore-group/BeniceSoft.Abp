using BeniceSoft.Core.Reflector;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace BeniceSoft.Abp.EntityFrameworkCore;

/// <summary>
/// 同一 DbContext 上挂起的 Future 批。
/// <para>
/// <b>原理：</b>多查询时把各 LINQ 编译出的 SQL 拼成一条多语句命令（参数重命名避免冲突），
/// 一次 ExecuteReader，再按 NextResult 依次物化；单查询或禁用批处理时退回逐条执行。
/// </para>
/// </summary>
[SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Batch SQL uses EF relational parameter internals.")]
public class QueryFutureBatch(DbContext ctx)
{
    public DbContext Context { get; set; } = ctx;

    public bool IsInMemory { get; set; }

    public List<BaseQueryFuture> Queries { get; set; } = [];

    public void ExecuteQueries()
    {
        if (Queries.Count == 0)
        {
            return;
        }

        if (IsInMemory)
        {
            foreach (var query in Queries)
            {
                query.ExecuteInMemory();
            }

            Queries.Clear();
            return;
        }

        if (Queries.Count == 1)
        {
            Queries[0].GetResultDirectly();
            Queries.Clear();
            return;
        }

        var allowQueryBatch = QueryFutureManager.AllowQueryBatch;

        if (!allowQueryBatch)
        {
            foreach (var query in Queries)
            {
                query.GetResultDirectly();
            }

            Queries.Clear();
            return;
        }

        var connection = Context.Database.GetDbConnection();
        var firstQuery = Queries[0];

        try
        {
            var command = CreateCommandCombined();
            var ownConnection = false;

            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                    ownConnection = true;
                }

                using (command)
                {
                    QueryFutureManager.OnBatchExecuting?.Invoke(command);
                    using var reader = command.ExecuteReader();
                    var createEntityDataReader = new CreateEntityDataReader(reader);
                    foreach (var query in Queries)
                    {
                        query.SetResult(createEntityDataReader);
                        reader.NextResult();
                    }

                    QueryFutureManager.OnBatchExecuted?.Invoke(command);
                }

                Queries.Clear();
            }
            finally
            {
                if (ownConnection)
                {
                    connection.Close();
                }
            }
        }
        finally
        {
            firstQuery.RestoreConnection?.Invoke();
        }
    }

    public async Task ExecuteQueriesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Queries.Count == 0)
        {
            return;
        }

        if (IsInMemory)
        {
            foreach (var query in Queries)
            {
                query.ExecuteInMemory();
            }

            Queries.Clear();
            return;
        }

        if (Queries.Count == 1)
        {
            await Queries[0].GetResultDirectlyAsync(cancellationToken);
            Queries.Clear();
            return;
        }

        if (!QueryFutureManager.AllowQueryBatch)
        {
            foreach (var query in Queries)
            {
                await query.GetResultDirectlyAsync(cancellationToken);
            }

            Queries.Clear();
            return;
        }

        var connection = Context.Database.GetDbConnection();
        var firstQuery = Queries[0];

        try
        {
            var command = CreateCommandCombined();
            var ownConnection = false;

            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                    ownConnection = true;
                }

                using (command)
                {
                    QueryFutureManager.OnBatchExecuting?.Invoke(command);
                    using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    var createEntityDataReader = new CreateEntityDataReader(reader);
                    foreach (var query in Queries)
                    {
                        query.SetResult(createEntityDataReader);
                        await reader.NextResultAsync(cancellationToken);
                    }

                    QueryFutureManager.OnBatchExecuted?.Invoke(command);
                }

                Queries.Clear();
            }
            finally
            {
                if (ownConnection)
                {
                    await connection.CloseAsync();
                }
            }
        }
        finally
        {
            firstQuery.RestoreConnection?.Invoke();
        }
    }

    /// <summary>
    /// 合并各 Future 的 SQL：参数改为 Z_{n}_{old}，语句以分号拼接；Oracle 使用 open :cursor for。
    /// </summary>
    protected DbCommand CreateCommandCombined()
    {
        var command = Context.CreateStoreCommand();
        var sb = new StringBuilder();
        var queryCount = 1;

        var isOracle = command.GetType().FullName?.Contains("Oracle.DataAccess") == true;
        var isOracleManaged = command.GetType().FullName?.Contains("Oracle.ManagedDataAccess") == true;
        var isOracleDevArt = command.GetType().FullName?.Contains("Devart") == true;
        var isPostgreSQL = command.GetType().FullName?.Contains("Npgsql") == true;

        foreach (var query in Queries)
        {
            var queryCommand = query.CreateExecutorAndGetCommand(out var queryContext);
            var sql = queryCommand.CommandText;
            IReadOnlyList<IRelationalParameter> parameterList = queryCommand.Parameters;
            var invariantName = string.Empty;

            if (parameterList.Count == 1 && parameterList[0] is CompositeRelationalParameter compositeRelationalParameter)
            {
                invariantName = parameterList[0].InvariantName;
                parameterList = compositeRelationalParameter.RelationalParameters;
            }

            var i = 0;
            foreach (var relationalParameter in parameterList)
            {
                object? value = null;
                // EF10：参数字典为 QueryContext.Parameters
                var parameterKey = string.IsNullOrEmpty(invariantName) ? relationalParameter.InvariantName : invariantName;
                var parameter = queryContext.Parameters[parameterKey];

                MethodInfo? methodConvertFromProvider = null;
                object? convertToProvider = null;

                var propertyRelationalTypeMapping = relationalParameter.GetType().GetProperty(
                    "RelationalTypeMapping",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                if (propertyRelationalTypeMapping != null)
                {
                    var relationalTypeMapping = propertyRelationalTypeMapping.GetValue(relationalParameter);
                    var propertyConverter = relationalTypeMapping?.GetType().GetProperty(
                        "Converter",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);

                    if (propertyConverter != null)
                    {
                        var converter = propertyConverter.GetValue(relationalTypeMapping);
                        var propertyConvertToProvider = converter?.GetType().GetProperty(
                            "ConvertToProvider",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);

                        if (propertyConvertToProvider != null)
                        {
                            convertToProvider = propertyConvertToProvider.GetValue(converter);
                            methodConvertFromProvider = convertToProvider?.GetType().GetMethod(
                                "Invoke",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                        }
                        else
                        {
                            var spatialPropertyConverter = relationalTypeMapping?.GetType().GetProperty(
                                "SpatialConverter",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                            if (spatialPropertyConverter != null)
                            {
                                var converterSpatial = spatialPropertyConverter.GetValue(relationalTypeMapping);
                                var spatialPropertyConvertToProvider = converterSpatial?.GetType().GetProperty(
                                    "ConvertToProvider",
                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);

                                if (spatialPropertyConvertToProvider != null)
                                {
                                    convertToProvider = spatialPropertyConvertToProvider.GetValue(converterSpatial);
                                    methodConvertFromProvider = convertToProvider?.GetType().GetMethod(
                                        "Invoke",
                                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                                }
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(invariantName) && parameter is object[] objectArray)
                {
                    value = objectArray[i];
                    i++;
                }

                string oldValue;
                if (relationalParameter is TypeMappedRelationalParameter parameterToCheck
                    && parameterToCheck.Name != null
                    && parameterToCheck.Name.StartsWith("@_")
                    && parameterToCheck.Name[1..] != relationalParameter.InvariantName)
                {
                    oldValue = parameterToCheck.Name[1..];
                }
                else
                {
                    oldValue = relationalParameter.InvariantName;
                }

                var newValue = string.Concat("Z_", queryCount, "_", oldValue);

                var dbParameter = command.CreateParameter();
                dbParameter.CopyFrom(relationalParameter, value ?? parameter, newValue);

                if (methodConvertFromProvider != null)
                {
                    dbParameter.Value = methodConvertFromProvider.Invoke(convertToProvider, [dbParameter.Value]);
                }

                if (dbParameter.Value == null || dbParameter.Value.GetType() != typeof(object[]) || ((object[])dbParameter.Value).Length != 0)
                {
                    command.Parameters.Add(dbParameter);
                }

                if (isPostgreSQL)
                {
                    var relationalTypeMappingProperty = typeof(TypeMappedRelationalParameter).GetProperty(
                        "RelationalTypeMapping",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (relationalTypeMappingProperty != null)
                    {
                        var relationalTypeMapping = (RelationalTypeMapping?)relationalTypeMappingProperty.GetValue(relationalParameter);
                        if (relationalTypeMapping != null
                            && relationalTypeMapping.StoreType.Equals("citext", StringComparison.OrdinalIgnoreCase))
                        {
                            var propertyPostgreSQLDBType = dbParameter.GetType().GetProperty("NpgsqlDbType", BindingFlags.Public | BindingFlags.Instance);
                            if (propertyPostgreSQLDBType != null)
                            {
                                propertyPostgreSQLDBType.SetValue(dbParameter, 51); // NpgsqlDbType.Citext
                            }
                            else
                            {
                                throw new InvalidOperationException(
                                    "NpgsqlDbType was not found when resolving citext for QueryFuture.");
                            }
                        }
                    }
                }

                if (isOracle || isOracleManaged || isOracleDevArt)
                {
                    sql = sql.Replace(":" + oldValue, ":" + newValue);
                }
                else
                {
                    sql = sql.Replace("@" + oldValue, "@" + newValue);
                }
            }

            sb.AppendLine(string.Concat("-- BeniceSoft Query Future: ", queryCount, " of ", Queries.Count));

            if (isOracle || isOracleManaged || isOracleDevArt)
            {
                var parameterName = "k_cursor_" + queryCount;
                sb.AppendLine("open :" + parameterName + " for " + sql);
                var param = command.CreateParameter();
                param.ParameterName = parameterName;
                param.Direction = ParameterDirection.Output;
                param.Value = DBNull.Value;

                if (isOracle)
                {
                    SetOracleDbType(command.GetType().Assembly, param, 121);
                }
                else if (isOracleManaged)
                {
                    SetOracleManagedDbType(command.GetType().Assembly, param, 121);
                }
                else if (isOracleDevArt)
                {
                    SetOracleDevArtDbType(command.GetType().Assembly, param, 7);
                }

                command.Parameters.Add(param);
            }
            else
            {
                sb.AppendLine(sql);
            }

            sb.Append(';');
            sb.AppendLine();
            sb.AppendLine();

            queryCount++;
        }

        command.CommandText = sb.ToString();

        if (isOracle || isOracleManaged || isOracleDevArt)
        {
            var bindByNameProperty = command.GetType().GetProperty("BindByName")
                                     ?? command.GetType().GetProperty("PassParametersByName");
            bindByNameProperty!.GetReflector().SetValue(command, true);
            command.CommandText = "BEGIN" + Environment.NewLine + command.CommandText + Environment.NewLine + "END;";
        }

        return command;
    }

    private static Action<DbParameter, object>? _setOracleDbType;
    private static Action<DbParameter, object>? _setOracleManagedDbType;
    private static Action<DbParameter, object>? _setOracleDevArtDbType;

    public static void SetOracleManagedDbType(Assembly assembly, DbParameter dbParameter, object type)
    {
        _setOracleManagedDbType ??= BuildOracleDbTypeSetter(
            assembly,
            "Oracle.ManagedDataAccess.Client.OracleDbType",
            "Oracle.ManagedDataAccess.Client.OracleParameter");
        _setOracleManagedDbType(dbParameter, type);
    }

    public static void SetOracleDbType(Assembly assembly, DbParameter dbParameter, object type)
    {
        _setOracleDbType ??= BuildOracleDbTypeSetter(
            assembly,
            "Oracle.DataAccess.Client.OracleDbType",
            "Oracle.DataAccess.Client.OracleParameter");
        _setOracleDbType(dbParameter, type);
    }

    public static void SetOracleDevArtDbType(Assembly assembly, DbParameter dbParameter, object type)
    {
        _setOracleDevArtDbType ??= BuildOracleDbTypeSetter(
            assembly,
            "Devart.Data.Oracle.OracleDbType",
            "Devart.Data.Oracle.OracleParameter");
        _setOracleDevArtDbType(dbParameter, type);
    }

    private static Action<DbParameter, object> BuildOracleDbTypeSetter(Assembly assembly, string dbTypeName, string parameterTypeName)
    {
        var dbtype = assembly.GetType(dbTypeName)!;
        var dbParameterType = assembly.GetType(parameterTypeName)!;
        var propertyInfo = dbParameterType.GetProperty("OracleDbType")!;

        var parameter = Expression.Parameter(typeof(DbParameter));
        var parameterConvert = Expression.Convert(parameter, dbParameterType);
        var parameterValue = Expression.Parameter(typeof(object));
        var parameterValueConvert = Expression.Convert(parameterValue, dbtype);
        var property = Expression.Property(parameterConvert, propertyInfo);
        var expression = Expression.Assign(property, parameterValueConvert);

        return Expression.Lambda<Action<DbParameter, object>>(expression, parameter, parameterValue).Compile();
    }
}

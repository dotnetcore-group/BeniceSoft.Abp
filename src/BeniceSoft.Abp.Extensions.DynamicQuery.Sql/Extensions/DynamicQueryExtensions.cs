using BeniceSoft.Core;
using BeniceSoft.Core.Constants;
using BeniceSoft.Extensions.DynamicQuery;
using SqlKata;
using SqlKata.Compilers;

namespace BeniceSoft.Abp.Extensions.DynamicQuery.Sql.Extensions;

public static class DynamicQueryExtensions
{
    public static SqlResult DynamicQueryBy(this string sql, IDynamicQueryRequest request, SqlCompilerType compilerType = SqlCompilerType.SqlServer)
    {
        var compiler = GetSqlCompiler(compilerType);
        return DynamicQueryBy(sql, request, compiler);
    }

    public static SqlResult DynamicQueryBy(this string sql, IDynamicQueryRequest request, Compiler compiler)
    {
        var tempSubQueryAlias = $"__dyq_temp_table";
        var query = new Query().FromRaw($"({sql}) as {tempSubQueryAlias}");
        AppendConditions(query, request);

        return compiler.Compile(query);
    }

    private static void AppendConditions(Query query, IDynamicQueryRequest request)
    {
        query.WhereRaw("1=1");
        if (!(request.ConditionGroups?.Any() ?? false)) return;

        for (var i = 0; i < request.ConditionGroups.Count; i++)
        {
            var conditionGroup = request.ConditionGroups[i];

            if (i > 0 && conditionGroup.Relation == BeniceSoftRelationConstant.Or)

            {
                query = query.Or();
            }

            query.Where(q =>
            {
                for (var j = 0; j < conditionGroup.Conditions.Count; j++)
                {
                    var condition = conditionGroup.Conditions[j];

                    if (j > 0 && condition.Relation == BeniceSoftRelationConstant.Or)
                    {
                        q.Or();
                    }

                    q = BuildOperatorExpress(q, condition);
                }

                return q;
            });
        }
    }

    private static Compiler GetSqlCompiler(SqlCompilerType compilerType) => compilerType switch
    {
        SqlCompilerType.SqlServer => new SqlServerCompiler(),
        SqlCompilerType.MySql => new MySqlCompiler(),
        SqlCompilerType.Postgres => new PostgresCompiler(),
        SqlCompilerType.Oracle => new OracleCompiler(),
        SqlCompilerType.Sqlite => new SqliteCompiler(),
        SqlCompilerType.Firebird => new FirebirdCompiler(),
        _ => throw new DynamicQueryException("不支持的 SqlCompilerType")
    };

    private static Query BuildOperatorExpress(Query q, DynamicQueryCondition condition)
    {
        return condition.Operator switch
        {
            ExprOperator.Equal => Equal(q, condition),
            ExprOperator.NotEqual => NotEqual(q, condition),
            ExprOperator.In => In(q, condition),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static Query Equal(Query query, DynamicQueryCondition condition)
    {
        var value = GetFormattedValue(condition.FieldType, condition.Value.FirstOrDefault());

        return query.Where(condition.FieldName, "=", value);
    }

    private static Query NotEqual(Query query, DynamicQueryCondition condition)
    {
        var value = GetFormattedValue(condition.FieldType, condition.Value.FirstOrDefault());

        return query.Where(condition.FieldName, "<>", value);
    }

    private static Query In(Query query, DynamicQueryCondition condition)
    {
        var values = condition.Value.Select(x => GetFormattedValue(condition.FieldType, x));

        return query.WhereIn(condition.FieldName, values);
    }


    private static string GetFormattedValue(string fieldType, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new DynamicQueryException("value is null or empty");
        if (fieldType == BeniceSoftTypeNameConstant.DateTime ||
            fieldType == BeniceSoftTypeNameConstant.Date ||
            fieldType == BeniceSoftTypeNameConstant.Guid ||
            fieldType == BeniceSoftTypeNameConstant.String)
        {
            return $"'{value}'";
        }

        return value;
    }
}
using BeniceSoft.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;

namespace BeniceSoft.Abp.EntityFrameworkCore.PostgreSql;

public static class TableHintExtensions
{
    internal const string AnnotationTag = "PG_HINT";

    private static long _counter;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Row lock hint requires custom SQL generator.")]
    public static DbContextOptionsBuilder ForRowState(this DbContextOptionsBuilder optionsBuilder)
    {
        var extension = optionsBuilder.Options.FindExtension<NpgsqlOptionsExtension>();
        if (extension == null)
        {
            return optionsBuilder;
        }

        return optionsBuilder.ReplaceService<IQuerySqlGeneratorFactory, HintNpgsqlQuerySqlGeneratorFactory>();
    }

    public static IQueryable<T> ForUpdate<T>(this IQueryable<T> query, bool ofAlias = true) where T : class
        => query.ForHint("UPDATE", ofAlias);

    public static IQueryable<T> ForShare<T>(this IQueryable<T> query, bool ofAlias = true) where T : class
        => query.ForHint("SHARE", ofAlias);

    public static IQueryable<T> ForHint<T>(this IQueryable<T> query, string hint, bool ofAlias = true)
        where T : class
    {
        if (hint.IsNull())
        {
            return query;
        }

        var expr = query.GetSelectExpression();
        if (expr.Tables.IsNull())
        {
            throw new InvalidDataException("not found tables");
        }

        var tabExpr = expr.Tables[0] as TableExpression;
        if (tabExpr?.Table == null)
        {
            throw new InvalidDataException("not found table base object");
        }

        if (ofAlias)
        {
            if (tabExpr.Alias.IsNull())
            {
                throw new InvalidDataException("table not set alias");
            }

            hint = $"{hint} OF {tabExpr.Alias}";
        }

        return query.TagWith($"{AnnotationTag}:{Interlocked.Increment(ref _counter)}:{hint}");
    }

    public static IQueryable<T> ForUpdateOf<T>(this IQueryable<T> query, params Type[] types) where T : class
        => query.ForHintOf("UPDATE", types);

    public static IQueryable<T> ForShareOf<T>(this IQueryable<T> query, params Type[] types) where T : class
        => query.ForHintOf("SHARE", types);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "FOR UPDATE OF alias resolution.")]
    public static IQueryable<T> ForHintOf<T>(this IQueryable<T> query, string hint, params Type[] types)
        where T : class
    {
        if (types.IsNull() || types.Length == 0)
        {
            return query;
        }

        var expr = query.GetSelectExpression();
        if (expr.Tables.IsNull())
        {
            throw new InvalidDataException("not found tables");
        }

        foreach (var type in types)
        {
            for (var i = expr.Tables.Count - 1; i >= 0; i--)
            {
                TableExpression? tabExpr;
                if (i > 0)
                {
                    var joinExpr = expr.Tables[i] as JoinExpressionBase;
                    tabExpr = joinExpr?.Table as TableExpression;
                }
                else
                {
                    tabExpr = expr.Tables[i] as TableExpression;
                }

                if (tabExpr?.Table == null)
                {
                    throw new InvalidDataException($"not found {type.Name} table base object");
                }

                var mapping = tabExpr.Table.EntityTypeMappings.OfType<TableMapping>().FirstOrDefault();
                if (mapping == null)
                {
                    throw new InvalidDataException($"not found {type.Name} table mapping");
                }

                if (mapping.TypeBase.ClrType == type)
                {
                    if (tabExpr.Alias.IsNull())
                    {
                        throw new InvalidDataException($"{type.Name} table not set alias");
                    }

                    var tagged = $"{hint} OF {tabExpr.Alias}";
                    query = query.TagWith($"{AnnotationTag}:{Interlocked.Increment(ref _counter)}:{tagged}");
                    break;
                }

                if (i == 0)
                {
                    throw new InvalidDataException($"not found {type.Name} table base object");
                }
            }
        }

        return query;
    }
}

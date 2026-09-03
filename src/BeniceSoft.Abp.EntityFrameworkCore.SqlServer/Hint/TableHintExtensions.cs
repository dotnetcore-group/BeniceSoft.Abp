using BeniceSoft.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
using System.Collections.Concurrent;

namespace BeniceSoft.Abp.EntityFrameworkCore.SqlServer;

public static class TableHintExtensions
{
    internal const string AnnotationTag = "MS_HINT";

    private static long _counter;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Table hint requires custom SQL generator.")]
    public static DbContextOptionsBuilder WithTableHint(this DbContextOptionsBuilder optionsBuilder)
    {
        var extension = optionsBuilder.Options.FindExtension<SqlServerOptionsExtension>();
        if (extension == null)
        {
            return optionsBuilder;
        }

        return optionsBuilder.ReplaceService<IQuerySqlGeneratorFactory, HintSqlServerQuerySqlGeneratorFactory>();
    }

    public static IQueryable<T> WithNoLock<T>(this IQueryable<T> query, params Type[] types) where T : class
        => query.WithHint("NOLOCK", types);

    public static IQueryable<T> WithReadPast<T>(this IQueryable<T> query, params Type[] types) where T : class
        => query.WithHint("READPAST", types);

    public static IQueryable<T> WithUpdLock<T>(this IQueryable<T> query, params Type[] types) where T : class
        => query.WithHint("UPDLOCK", types);

    public static IQueryable<T> WithIndex<T>(this IQueryable<T> query, string indexName, params Type[] types) where T : class
        => query.WithHint($"Index({indexName})", types);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Table hint annotates ITableBase.")]
    public static IQueryable<T> WithHint<T>(this IQueryable<T> query, string hint, params Type[] types)
        where T : class
    {
        if (hint.IsNull())
        {
            return query;
        }

        void AddTag(ITableBase table)
        {
            var tag = Interlocked.Increment(ref _counter).ToString();
            lock (table)
            {
                var hints = table.FindRuntimeAnnotationValue(AnnotationTag) as ConcurrentDictionary<string, string> ?? new();
                hints.TryAdd(tag, $" WITH ({hint})");
                table.SetRuntimeAnnotation(AnnotationTag, hints);
            }

            query = query.TagWith($"{AnnotationTag}:{tag}");
        }

        var expr = query.GetSelectExpression();
        if (expr.Tables.IsNull())
        {
            throw new InvalidDataException("not found tables");
        }

        if (types.IsNull() || types.Length == 0)
        {
            var tabExpr = expr.Tables[0] as TableExpression;
            if (tabExpr?.Table == null)
            {
                throw new InvalidDataException("not found table base object");
            }

            AddTag(tabExpr.Table);
            return query;
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
                    AddTag(tabExpr.Table);
                    break;
                }

                if (i == 0)
                {
                    throw new InvalidDataException($"not found {type.Name} table object");
                }
            }
        }

        return query;
    }
}

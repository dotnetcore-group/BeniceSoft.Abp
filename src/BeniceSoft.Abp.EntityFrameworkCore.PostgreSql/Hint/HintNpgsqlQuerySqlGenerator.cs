using BeniceSoft.Core;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;
using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.PostgreSql;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Custom FOR UPDATE/SHARE SQL generator.")]
internal sealed class HintNpgsqlQuerySqlGenerator(
    QuerySqlGeneratorDependencies dependencies,
    IRelationalTypeMappingSource typeMappingSource,
    bool reverseNullOrderingEnabled,
    Version postgresVersion)
    : NpgsqlQuerySqlGenerator(dependencies, typeMappingSource, reverseNullOrderingEnabled, postgresVersion)
{
    private const string OfString = " OF ";

    protected override Expression VisitSelect(SelectExpression selectExpression)
    {
        var expr = base.VisitSelect(selectExpression);

        if (selectExpression.Tags.IsNull())
        {
            return expr;
        }

        var tags = selectExpression.Tags
            .Where(t => t != null && t.StartsWith($"{TableHintExtensions.AnnotationTag}:"))
            .Select(t => t[(t.LastIndexOf(':') + 1)..])
            .ToList();

        if (tags.IsNull())
        {
            return expr;
        }

        var dic = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var state in tags)
        {
            var index = state.IndexOf(OfString, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                var key = state[..index];
                var alias = state[(index + OfString.Length)..];
                if (!dic.TryGetValue(key, out var sets))
                {
                    dic.Add(key, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { alias });
                }
                else
                {
                    sets.Add(alias);
                }
            }
            else
            {
                Sql.Append($" FOR {state}");
            }
        }

        foreach (var item in dic)
        {
            Sql.Append($" FOR {item.Key} OF {item.Value.JoinStr()}");
        }

        return expr;
    }
}

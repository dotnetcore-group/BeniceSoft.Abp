using BeniceSoft.Core;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.SqlServer;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Custom table hint SQL generator.")]
internal sealed class HintSqlServerQuerySqlGenerator(
    QuerySqlGeneratorDependencies dependencies,
    IRelationalTypeMappingSource typeMappingSource,
    ISqlServerSingletonOptions sqlServerSingletonOptions)
    : SqlServerQuerySqlGenerator(dependencies, typeMappingSource, sqlServerSingletonOptions)
{
    private List<string> _tags = [];

    protected override Expression VisitSelect(SelectExpression selectExpression)
    {
        _tags = selectExpression.Tags
            .Where(t => t != null && t.StartsWith($"{TableHintExtensions.AnnotationTag}:"))
            .Select(t => t[(t.LastIndexOf(':') + 1)..])
            .ToList();
        return base.VisitSelect(selectExpression);
    }

    protected override Expression VisitTable(TableExpression tableExpression)
    {
        var expr = base.VisitTable(tableExpression);

        lock (tableExpression.Table)
        {
            if (tableExpression.Table?.FindRuntimeAnnotationValue(TableHintExtensions.AnnotationTag) is ConcurrentDictionary<string, string> hints && !hints.IsEmpty)
            {
                var tag = string.Empty;
                foreach (var t in _tags)
                {
                    if (hints.TryRemove(t, out var hint))
                    {
                        tag = t;
                        Sql.Append(hint);
                        break;
                    }
                }

                if (tag.IsNotNull())
                {
                    _tags.Remove(tag);
                    if (hints.IsEmpty)
                    {
                        tableExpression.Table.RemoveRuntimeAnnotation(TableHintExtensions.AnnotationTag);
                    }
                }
            }
        }

        return expr;
    }
}

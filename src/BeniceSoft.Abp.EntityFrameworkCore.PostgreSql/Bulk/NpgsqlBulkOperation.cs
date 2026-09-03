using BeniceSoft.Abp.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore.PostgreSql.Bulk;

/// <summary>PostgreSQL 多步 Bulk 会话：工厂产出 <see cref="NpgsqlBulkAtom{T}"/>（COPY + UPDATE/DELETE/ON CONFLICT）。</summary>
public class NpgsqlBulkOperation : EfCoreBulkOperation
{
    public NpgsqlBulkOperation(DbContext ctx)
        : base(ctx)
    {
    }

    protected override EfCoreBulkAtom<T> CreateAtom<T>(IEnumerable<T> items)
        => new NpgsqlBulkAtom<T>(DbContext, items);
}

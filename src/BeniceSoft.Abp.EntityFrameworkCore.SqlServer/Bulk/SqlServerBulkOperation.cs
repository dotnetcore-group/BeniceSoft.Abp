using BeniceSoft.Abp.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore.SqlServer.Bulk;

/// <summary>SQL Server 多步 Bulk 会话：工厂产出 <see cref="SqlServerBulkAtom{T}"/>（SqlBulkCopy + MERGE）。</summary>
public class SqlServerBulkOperation : EfCoreBulkOperation
{
    public SqlServerBulkOperation(DbContext ctx)
        : base(ctx)
    {
    }

    protected override EfCoreBulkAtom<T> CreateAtom<T>(IEnumerable<T> items)
        => new SqlServerBulkAtom<T>(DbContext, items);
}

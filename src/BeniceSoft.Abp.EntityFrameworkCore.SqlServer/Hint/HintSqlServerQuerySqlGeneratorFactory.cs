using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace BeniceSoft.Abp.EntityFrameworkCore.SqlServer;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Custom table hint SQL generator factory.")]
internal sealed class HintSqlServerQuerySqlGeneratorFactory(
    QuerySqlGeneratorDependencies dependencies,
    IRelationalTypeMappingSource typeMappingSource,
    ISqlServerSingletonOptions sqlServerSingletonOptions)
    : SqlServerQuerySqlGeneratorFactory(dependencies, typeMappingSource, sqlServerSingletonOptions)
{
    private readonly IRelationalTypeMappingSource _typeMappingSource = typeMappingSource;
    private readonly ISqlServerSingletonOptions _sqlServerSingletonOptions = sqlServerSingletonOptions;

    public override QuerySqlGenerator Create()
        => new HintSqlServerQuerySqlGenerator(Dependencies, _typeMappingSource, _sqlServerSingletonOptions);
}

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;

namespace BeniceSoft.Abp.EntityFrameworkCore.PostgreSql;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Custom FOR UPDATE/SHARE SQL generator factory.")]
internal sealed class HintNpgsqlQuerySqlGeneratorFactory(
    QuerySqlGeneratorDependencies dependencies,
    IRelationalTypeMappingSource typeMappingSource,
    INpgsqlSingletonOptions npgsqlSingletonOptions)
    : NpgsqlQuerySqlGeneratorFactory(dependencies, typeMappingSource, npgsqlSingletonOptions)
{
    private readonly QuerySqlGeneratorDependencies _dependencies = dependencies;
    private readonly IRelationalTypeMappingSource _typeMappingSource = typeMappingSource;
    private readonly INpgsqlSingletonOptions _npgsqlSingletonOptions = npgsqlSingletonOptions;

    public override QuerySqlGenerator Create()
        => new HintNpgsqlQuerySqlGenerator(
            _dependencies,
            _typeMappingSource,
            _npgsqlSingletonOptions.ReverseNullOrderingEnabled,
            _npgsqlSingletonOptions.PostgresVersion);
}

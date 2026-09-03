using DtmCommon;
using JetBrains.Annotations;

namespace BeniceSoft.Abp.EventBus.Dtm.EntityFrameworkCore;

public interface IDtmBarrierDbSpecial : IDbSpecial
{
    string GetCreateBarrierTableSql(DtmEventBoxesOptions options);

    string GetInsertIgnoreTemplate([NotNull] string tableName);

    string GetQueryPreparedSql([NotNull] string tableName);
}

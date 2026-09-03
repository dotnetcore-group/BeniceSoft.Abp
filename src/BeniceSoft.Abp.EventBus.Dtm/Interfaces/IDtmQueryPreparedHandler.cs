using JetBrains.Annotations;

namespace BeniceSoft.Abp.EventBus.Dtm;

public interface IDtmQueryPreparedHandler
{
    Task<bool> CanHandleAsync([NotNull] string dbContextTypeName);

    Task<bool> TryInsertBarrierAsRollbackAsync(
        [NotNull] string dbContextTypeName,
        [NotNull] string hashedConnectionString,
        [NotNull] string gid);
}

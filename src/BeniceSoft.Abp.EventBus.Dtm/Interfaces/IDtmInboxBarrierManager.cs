using JetBrains.Annotations;

namespace BeniceSoft.Abp.EventBus.Dtm;

public interface IDtmInboxBarrierManager<in TDbContextInterface> where TDbContextInterface : class
{
    Task EnsureInsertBarrierAsync(TDbContextInterface dbContext, [NotNull] string gid);

    Task<bool> ExistBarrierAsync(TDbContextInterface dbContext, [NotNull] string gid);
}

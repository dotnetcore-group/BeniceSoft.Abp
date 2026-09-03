namespace BeniceSoft.Abp.EventBus.Dtm;

public interface IDtmBranchBarrierManager
{
    Task<DtmBranchBarrierInsertResult> TryInsertBarrierAsync(
        DtmBranchBarrierInfo barrierInfo,
        string? dbContextTypeName,
        string? hashedConnectionString,
        CancellationToken cancellationToken = default);
}

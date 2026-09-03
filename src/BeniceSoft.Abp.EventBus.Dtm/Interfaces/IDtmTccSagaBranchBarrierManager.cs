namespace BeniceSoft.Abp.EventBus.Dtm;

/// <summary>
/// TCC 分支屏障管理器。
/// </summary>
public interface ITccBranchBarrierManager : IDtmBranchBarrierManager
{
}

/// <summary>
/// SAGA 分支屏障管理器。
/// </summary>
public interface ISagaBranchBarrierManager : IDtmBranchBarrierManager
{
}

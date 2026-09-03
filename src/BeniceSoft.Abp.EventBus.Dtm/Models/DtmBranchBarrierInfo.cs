namespace BeniceSoft.Abp.EventBus.Dtm;

public class DtmBranchBarrierInfo
{
    public string Gid { get; set; } = string.Empty;

    public string TransType { get; set; } = string.Empty;

    public string BranchId { get; set; } = string.Empty;

    public string Op { get; set; } = string.Empty;

    public string BarrierId { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}

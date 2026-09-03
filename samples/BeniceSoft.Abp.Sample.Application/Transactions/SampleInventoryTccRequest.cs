using BeniceSoft.Abp.EventBus.Dtm.Http;

namespace BeniceSoft.Abp.Sample.Application;

[DtmBranch("inventory-service", "inventory-reserve")]
public class SampleInventoryTccRequest : IBranchRequest
{
    public string BizId { get; set; } = string.Empty;

    public string ProductCode { get; set; } = string.Empty;

    public int Quantity { get; set; }
}


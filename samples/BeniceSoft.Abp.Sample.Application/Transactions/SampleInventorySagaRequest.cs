using BeniceSoft.Abp.EventBus.Dtm.Http;

namespace BeniceSoft.Abp.Sample.Application;

[DtmBranch("inventory-service", "inventory-deduct")]
public class SampleInventorySagaRequest : IBranchRequest
{
    public string BizId { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool SimulateBizFailure { get; set; }
}

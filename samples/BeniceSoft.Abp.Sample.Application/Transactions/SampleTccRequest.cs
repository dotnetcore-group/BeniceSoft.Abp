using BeniceSoft.Abp.EventBus.Dtm.Http;

namespace BeniceSoft.Abp.Sample.Application;

[DtmBranch("order-service", "order-create")]
public class SampleTccRequest : IBranchRequest
{
    public string BizId { get; set; } = string.Empty;

    public string ProductCode { get; set; } = string.Empty;

    public int Quantity { get; set; }
}


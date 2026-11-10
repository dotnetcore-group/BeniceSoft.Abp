using BeniceSoft.Abp.EventBus.Dtm.Http;

namespace BeniceSoft.Abp.Sample.Application;

[DtmBranch("sample-service", "sample-order")]
public class SampleSagaRequest : IBranchRequest
{
    public string BizId { get; set; } = string.Empty;

    public string ProductCode { get; set; } = "SKU-1001";

    public int Quantity { get; set; } = 1;

    public decimal Amount { get; set; }

    public string PaymentChannel { get; set; } = string.Empty;
}


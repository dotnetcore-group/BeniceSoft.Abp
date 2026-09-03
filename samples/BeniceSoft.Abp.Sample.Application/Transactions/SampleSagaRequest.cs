using BeniceSoft.Abp.EventBus.Dtm.Http;

namespace BeniceSoft.Abp.Sample.Application;

[DtmBranch("sample-service", "sample-order")]
public class SampleSagaRequest : IBranchRequest
{
    public string BizId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string PaymentChannel { get; set; } = string.Empty;
}


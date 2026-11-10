using BeniceSoft.Abp.EventBus.Dtm.Http;
using BeniceSoft.Abp.Sample.Domain;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Sample.Application;

public class SampleOrderTccCallbackHandler : ITccBranchCallbackHandler<SampleTccRequest>, ITransientDependency
{
    private readonly ILogger<SampleOrderTccCallbackHandler> _logger;
    private readonly TransactionSampleManager _transactionSampleManager;

    public SampleOrderTccCallbackHandler(
        ILogger<SampleOrderTccCallbackHandler> logger,
        TransactionSampleManager transactionSampleManager)
    {
        _logger = logger;
        _transactionSampleManager = transactionSampleManager;
    }

    public Task TryAsync(SampleTccRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Order-TCC-Try] 预创建订单，BizId={BizId}, ProductCode={ProductCode}, Quantity={Quantity}",
            request.BizId, request.ProductCode, request.Quantity);
        return _transactionSampleManager.PrepareOrderAsync(request.BizId, request.ProductCode, request.Quantity);
    }

    public Task ConfirmAsync(SampleTccRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Order-TCC-Confirm] 订单确认创建，BizId={BizId}", request.BizId);
        return _transactionSampleManager.ConfirmOrderAsync(request.BizId, request.ProductCode, request.Quantity);
    }

    public Task CancelAsync(SampleTccRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Order-TCC-Cancel] 取消预创建订单，BizId={BizId}", request.BizId);
        return _transactionSampleManager.CancelOrderAsync(request.BizId, request.ProductCode, request.Quantity);
    }

}

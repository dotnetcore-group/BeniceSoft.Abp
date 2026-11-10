using BeniceSoft.Abp.EventBus.Dtm.Http;
using BeniceSoft.Abp.Sample.Domain;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Sample.Application;

public class SampleInventoryTccCallbackHandler : ITccBranchCallbackHandler<SampleInventoryTccRequest>, ITransientDependency
{
    private readonly ILogger<SampleInventoryTccCallbackHandler> _logger;
    private readonly TransactionSampleManager _transactionSampleManager;

    public SampleInventoryTccCallbackHandler(
        ILogger<SampleInventoryTccCallbackHandler> logger,
        TransactionSampleManager transactionSampleManager)
    {
        _logger = logger;
        _transactionSampleManager = transactionSampleManager;
    }

    public Task TryAsync(SampleInventoryTccRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Inventory-TCC-Try] 冻结库存，BizId={BizId}, ProductCode={ProductCode}, Quantity={Quantity}",
            request.BizId, request.ProductCode, request.Quantity);
        return _transactionSampleManager.PrepareInventoryAsync(request.BizId, request.ProductCode, request.Quantity);
    }

    public Task ConfirmAsync(SampleInventoryTccRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Inventory-TCC-Confirm] 扣减冻结库存，BizId={BizId}, ProductCode={ProductCode}, Quantity={Quantity}",
            request.BizId, request.ProductCode, request.Quantity);
        return _transactionSampleManager.ConfirmInventoryAsync(request.BizId, request.ProductCode, request.Quantity);
    }

    public Task CancelAsync(SampleInventoryTccRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Inventory-TCC-Cancel] 释放冻结库存，BizId={BizId}, ProductCode={ProductCode}, Quantity={Quantity}",
            request.BizId, request.ProductCode, request.Quantity);
        return _transactionSampleManager.CancelInventoryAsync(request.BizId, request.ProductCode, request.Quantity);
    }
}

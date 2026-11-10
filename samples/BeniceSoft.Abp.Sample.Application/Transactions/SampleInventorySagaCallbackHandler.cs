using BeniceSoft.Abp.EventBus.Dtm.Http;
using BeniceSoft.Abp.Sample.Domain;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Sample.Application;

public class SampleInventorySagaCallbackHandler : ISagaBranchCallbackHandler<SampleInventorySagaRequest>, ITransientDependency
{
    private readonly TransactionSampleManager _transactionSampleManager;
    private readonly ILogger<SampleInventorySagaCallbackHandler> _logger;

    public SampleInventorySagaCallbackHandler(
        TransactionSampleManager transactionSampleManager,
        ILogger<SampleInventorySagaCallbackHandler> logger)
    {
        _transactionSampleManager = transactionSampleManager;
        _logger = logger;
    }

    public Task ActionAsync(SampleInventorySagaRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SimulateBizFailure)
        {
            throw new BusinessException("Sample.Transaction.SimulatedFailure", "模拟库存业务拒绝，触发 Saga 补偿");
        }

        _logger.LogInformation("[Inventory-SAGA-Action] 扣减库存，BizId={BizId}", request.BizId);
        return _transactionSampleManager.DeductSagaInventoryAsync(request.BizId, request.ProductCode, request.Quantity);
    }

    public Task CompensateAsync(SampleInventorySagaRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Inventory-SAGA-Compensate] 恢复库存，BizId={BizId}", request.BizId);
        return _transactionSampleManager.CompensateSagaInventoryAsync(request.BizId, request.ProductCode, request.Quantity);
    }
}

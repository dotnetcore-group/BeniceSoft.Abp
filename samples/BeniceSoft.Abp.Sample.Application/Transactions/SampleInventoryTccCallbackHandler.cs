using BeniceSoft.Abp.EventBus.Dtm.Http;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Sample.Application;

public class SampleInventoryTccCallbackHandler : ITccBranchCallbackHandler<SampleInventoryTccRequest>, ITransientDependency
{
    private readonly ILogger<SampleInventoryTccCallbackHandler> _logger;

    public SampleInventoryTccCallbackHandler(ILogger<SampleInventoryTccCallbackHandler> logger)
    {
        _logger = logger;
    }

    public Task TryAsync(SampleInventoryTccRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Inventory-TCC-Try] 冻结库存，BizId={BizId}, ProductCode={ProductCode}, Quantity={Quantity}",
            request.BizId, request.ProductCode, request.Quantity);
        return Task.CompletedTask;
    }

    public Task ConfirmAsync(SampleInventoryTccRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Inventory-TCC-Confirm] 扣减冻结库存，BizId={BizId}, ProductCode={ProductCode}, Quantity={Quantity}",
            request.BizId, request.ProductCode, request.Quantity);
        return Task.CompletedTask;
    }

    public Task CancelAsync(SampleInventoryTccRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Inventory-TCC-Cancel] 释放冻结库存，BizId={BizId}, ProductCode={ProductCode}, Quantity={Quantity}",
            request.BizId, request.ProductCode, request.Quantity);
        return Task.CompletedTask;
    }
}

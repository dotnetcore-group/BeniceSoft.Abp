using BeniceSoft.Abp.EventBus.Dtm.Http;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Sample.Application;

public class SampleOrderTccCallbackHandler : ITccBranchCallbackHandler<SampleTccRequest>, ITransientDependency
{
    private readonly ILogger<SampleOrderTccCallbackHandler> _logger;

    public SampleOrderTccCallbackHandler(ILogger<SampleOrderTccCallbackHandler> logger)
    {
        _logger = logger;
    }

    public Task TryAsync(SampleTccRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Order-TCC-Try] 预创建订单，BizId={BizId}, ProductCode={ProductCode}, Quantity={Quantity}",
            request.BizId, request.ProductCode, request.Quantity);
        return Task.CompletedTask;
    }

    public Task ConfirmAsync(SampleTccRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Order-TCC-Confirm] 订单确认创建，BizId={BizId}", request.BizId);
        return Task.CompletedTask;
    }

    public Task CancelAsync(SampleTccRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Order-TCC-Cancel] 取消预创建订单，BizId={BizId}", request.BizId);
        return Task.CompletedTask;
    }

}

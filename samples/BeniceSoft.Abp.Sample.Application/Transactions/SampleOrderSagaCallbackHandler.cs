using BeniceSoft.Abp.EventBus.Dtm.Http;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Sample.Application;

public class SampleOrderSagaCallbackHandler : ISagaBranchCallbackHandler<SampleSagaRequest>, ITransientDependency
{
    private readonly ILogger<SampleOrderSagaCallbackHandler> _logger;

    public SampleOrderSagaCallbackHandler(ILogger<SampleOrderSagaCallbackHandler> logger)
    {
        _logger = logger;
    }

    public Task ActionAsync(SampleSagaRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[SAGA-Action] BizId={BizId}, Amount={Amount}, PaymentChannel={PaymentChannel}",
            request.BizId, request.Amount, request.PaymentChannel);
        return Task.CompletedTask;
    }

    public Task CompensateAsync(SampleSagaRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[SAGA-Compensate] BizId={BizId}", request.BizId);
        return Task.CompletedTask;
    }
}

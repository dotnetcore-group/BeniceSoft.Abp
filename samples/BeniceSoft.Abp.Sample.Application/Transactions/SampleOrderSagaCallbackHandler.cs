using BeniceSoft.Abp.EventBus.Dtm.Http;
using BeniceSoft.Abp.Sample.Domain;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Sample.Application;

public class SampleOrderSagaCallbackHandler : ISagaBranchCallbackHandler<SampleSagaRequest>, ITransientDependency
{
    private readonly ILogger<SampleOrderSagaCallbackHandler> _logger;
    private readonly TransactionSampleManager _transactionSampleManager;

    public SampleOrderSagaCallbackHandler(
        ILogger<SampleOrderSagaCallbackHandler> logger,
        TransactionSampleManager transactionSampleManager)
    {
        _logger = logger;
        _transactionSampleManager = transactionSampleManager;
    }

    public Task ActionAsync(SampleSagaRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[SAGA-Action] BizId={BizId}, Amount={Amount}, PaymentChannel={PaymentChannel}",
            request.BizId, request.Amount, request.PaymentChannel);
        return _transactionSampleManager.CreateSagaOrderAsync(
            request.BizId, request.ProductCode, request.Quantity, request.Amount, request.PaymentChannel);
    }

    public Task CompensateAsync(SampleSagaRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[SAGA-Compensate] BizId={BizId}", request.BizId);
        return _transactionSampleManager.CompensateSagaOrderAsync(
            request.BizId, request.ProductCode, request.Quantity, request.Amount, request.PaymentChannel);
    }
}

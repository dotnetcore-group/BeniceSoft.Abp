using BeniceSoft.Abp.EventBus.Dtm.Http;
using Microsoft.Extensions.Logging;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.Sample.Application.Services;

public class DistributedEventSampleAppService : SampleAppServiceBase
{
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IGlobalTransaction _globalTransaction;
    private readonly ILogger<DistributedEventSampleAppService> _logger;

    public DistributedEventSampleAppService(
        IDistributedEventBus distributedEventBus,
        IGlobalTransaction globalTransaction,
        ILogger<DistributedEventSampleAppService> logger)
    {
        _distributedEventBus = distributedEventBus;
        _globalTransaction = globalTransaction;
        _logger = logger;
    }

    /// <summary>
    /// 测试http dtm事件
    /// </summary>
    /// <returns></returns>
    [UnitOfWork]
    public async Task TestHttpDtmEventAsync()
    {
        var eventData = new SampleDtmHttpEvent { TestId = $"test-{DateTime.Now:yyyyMMddHHmmssfff}" };

        try
        {
            _logger.LogInformation("开始发布分布式事件: {EventName}, TestId={TestId}", nameof(SampleDtmHttpEvent), eventData.TestId);

            await _distributedEventBus.PublishAsync(eventData);

            _logger.LogInformation("发布分布式事件完成: {EventName}, TestId={TestId}", nameof(SampleDtmHttpEvent), eventData.TestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发布事件失败: {Type} - {Message}", ex.GetType().FullName, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 示例：把订单服务、库存服务看作两个独立微服务进行 TCC 编排。
    /// 入口只负责编排分支；各服务通过各自 handler 处理 Try/Confirm/Cancel。
    /// </summary>
    [UnitOfWork]
    public async Task<string> TestTccTwoServicesStyleAsync(bool simulateBizFailure = false)
    {
        var bizId = $"tcc-2svc-{DateTime.Now:yyyyMMddHHmmssfff}";

        await _globalTransaction.ExecuteTccAsync(async tcc =>
        {
            var orderRequest = new SampleTccRequest
            {
                BizId = bizId,
                ProductCode = "SKU-1001",
                Quantity = 1
            };

            var inventoryRequest = new SampleInventoryTccRequest
            {
                BizId = bizId,
                ProductCode = orderRequest.ProductCode,
                Quantity = orderRequest.Quantity
            };

            // serviceName / handlerName 默认从 Request 的 [DtmBranch] 特性解析。

            // 如需临时改路由，也可在 CallBranchAsync<TRequest>(..., serviceName, handlerName) 显式覆盖。
            await tcc.CallBranchAsync(orderRequest);
            await tcc.CallBranchAsync(inventoryRequest);

            if (simulateBizFailure)
            {
                throw new Exception("模拟业务异常：在 Try 阶段后抛错，DTM 将触发 Cancel 回调。");
            }
        });

        _logger.LogInformation("双服务风格 TCC 事务执行完成，BizId={BizId}", bizId);

        return bizId;
    }

    [UnitOfWork]
    public async Task<string> TestSagaAsync()
    {
        var bizId = $"saga-{DateTime.Now:yyyyMMddHHmmssfff}";

        await _globalTransaction.ExecuteSagaAsync(saga =>
        {
            var request = new SampleSagaRequest
            {
                BizId = bizId,
                Amount = 100,
                PaymentChannel = "WeChat"
            };

            saga.AddBranchByHandler(request);

            return Task.CompletedTask;
        });

        _logger.LogInformation("SAGA 事务执行完成，BizId={BizId}", bizId);

        return bizId;
    }


}


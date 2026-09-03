using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public class DtmSagaCallbackMiddleware : DtmCallbackMiddlewareBase
{
    public DtmSagaCallbackMiddleware(RequestDelegate next) : base(next)
    {
    }

    public Task InvokeAsync(
        HttpContext context,
        IOptions<DtmTransactionCallbackOptions> callbackOptions,
        IEnumerable<ISagaBranchBarrierManager> dtmBranchBarrierManagers,
        IActionApiTokenChecker actionApiTokenChecker,
        Volo.Abp.Uow.IUnitOfWorkManager unitOfWorkManager,
        ILogger<DtmSagaCallbackMiddleware> logger)
    {
        return HandleAsync(
            context,
            callbackOptions.Value,
            dtmBranchBarrierManagers,
            actionApiTokenChecker,
            unitOfWorkManager,
            logger);
    }

    protected override string GetCallbackPathPrefix(DtmTransactionCallbackOptions options)
    {
        return options.SagaCallbackPathPrefix;
    }

    protected override IDictionary<string, DtmTransactionCallbackRegistration> GetRegistrations(DtmTransactionCallbackOptions options)
    {
        return options.SagaHandlers;
    }

    protected override string? ResolveMethodName(string op)
    {
        return op.ToLowerInvariant() switch
        {
            "action" => nameof(ISagaBranchCallbackHandler<object>.ActionAsync),
            "compensate" => nameof(ISagaBranchCallbackHandler<object>.CompensateAsync),
            _ => null
        };
    }
}

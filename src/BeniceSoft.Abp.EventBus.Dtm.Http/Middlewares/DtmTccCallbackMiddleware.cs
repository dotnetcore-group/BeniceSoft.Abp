using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public class DtmTccCallbackMiddleware : DtmCallbackMiddlewareBase
{
    public DtmTccCallbackMiddleware(RequestDelegate next) : base(next)
    {
    }

    public Task InvokeAsync(
        HttpContext context,
        IOptions<DtmTransactionCallbackOptions> callbackOptions,
        IEnumerable<ITccBranchBarrierManager> dtmBranchBarrierManagers,
        IActionApiTokenChecker actionApiTokenChecker,
        Volo.Abp.Uow.IUnitOfWorkManager unitOfWorkManager,
        ILogger<DtmTccCallbackMiddleware> logger)
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
        return options.TccCallbackPathPrefix;
    }

    protected override IDictionary<string, DtmTransactionCallbackRegistration> GetRegistrations(DtmTransactionCallbackOptions options)
    {
        return options.TccHandlers;
    }

    protected override string? ResolveMethodName(string op)
    {
        return op.ToLowerInvariant() switch
        {
            "try" => nameof(ITccBranchCallbackHandler<object>.TryAsync),
            "confirm" => nameof(ITccBranchCallbackHandler<object>.ConfirmAsync),
            "cancel" => nameof(ITccBranchCallbackHandler<object>.CancelAsync),
            _ => null
        };
    }
}

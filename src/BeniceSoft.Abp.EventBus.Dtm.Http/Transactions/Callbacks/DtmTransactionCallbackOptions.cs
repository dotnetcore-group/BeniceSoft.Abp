using Volo.Abp;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public sealed record DtmTransactionCallbackRegistration(Type HandlerType, Type RequestType);

public class DtmTransactionCallbackOptions
{
    /// <summary>
    /// TCC 回调前缀，完整格式：{Prefix}/{handlerName}/{op}
    /// op: try/confirm/cancel
    /// </summary>
    public string TccCallbackPathPrefix { get; set; } = "/dtm_boxes.DtmHttpService/tcc";

    /// <summary>
    /// SAGA 回调前缀，完整格式：{Prefix}/{handlerName}/{op}
    /// op: action/compensate
    /// </summary>
    public string SagaCallbackPathPrefix { get; set; } = "/dtm_boxes.DtmHttpService/saga";

    public Dictionary<string, DtmTransactionCallbackRegistration> TccHandlers { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, DtmTransactionCallbackRegistration> SagaHandlers { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public void AddTccHandler(Type handlerType, Type requestType, string handlerName)
    {
        Check.NotNull(handlerType, nameof(handlerType));
        Check.NotNull(requestType, nameof(requestType));
        Check.NotNullOrWhiteSpace(handlerName, nameof(handlerName));

        if (!typeof(ITccBranchCallbackHandler<>).MakeGenericType(requestType).IsAssignableFrom(handlerType))
        {
            throw new AbpException($"处理器 {handlerType.FullName} 未实现 ITccBranchCallbackHandler<{requestType.FullName}>。");
        }

        if (TccHandlers.ContainsKey(handlerName))
        {
            throw new AbpException($"DTM TCC 回调处理器名称重复: {handlerName}");
        }

        TccHandlers[handlerName] = new DtmTransactionCallbackRegistration(handlerType, requestType);
    }

    public void AddSagaHandler(Type handlerType, Type requestType, string handlerName)
    {
        Check.NotNull(handlerType, nameof(handlerType));
        Check.NotNull(requestType, nameof(requestType));
        Check.NotNullOrWhiteSpace(handlerName, nameof(handlerName));

        if (!typeof(ISagaBranchCallbackHandler<>).MakeGenericType(requestType).IsAssignableFrom(handlerType))
        {
            throw new AbpException($"处理器 {handlerType.FullName} 未实现 ISagaBranchCallbackHandler<{requestType.FullName}>。");
        }

        if (SagaHandlers.ContainsKey(handlerName))
        {
            throw new AbpException($"DTM SAGA 回调处理器名称重复: {handlerName}");
        }

        SagaHandlers[handlerName] = new DtmTransactionCallbackRegistration(handlerType, requestType);
    }
}

using BeniceSoft.Core.Reflector;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

internal static class DtmBranchMetadataResolver
{
    public static (string ServiceName, string HandlerName) Resolve(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);

        var attribute = requestType.GetReflector().GetCustomAttributes(typeof(DtmBranchAttribute))
            .OfType<DtmBranchAttribute>()
            .FirstOrDefault();

        if (attribute is null)
        {
            throw new InvalidOperationException($"请求类型 {requestType.FullName} 未配置 [DtmBranch] 特性。");
        }

        var serviceName = attribute.ServiceName.Trim();
        var handlerName = attribute.HandlerName.Trim();

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new InvalidOperationException($"请求类型 {requestType.FullName} 的 [DtmBranch] 未配置 ServiceName。");
        }

        if (string.IsNullOrWhiteSpace(handlerName))
        {
            throw new InvalidOperationException($"请求类型 {requestType.FullName} 的 [DtmBranch] 未配置 HandlerName。");
        }

        return (serviceName, handlerName);
    }
}

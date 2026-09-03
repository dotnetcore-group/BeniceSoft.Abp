namespace BeniceSoft.Abp.EventBus.Dtm.Http;

/// <summary>
/// 标记分支请求 BranchRequest 的路由元数据
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class DtmBranchAttribute : Attribute
{
    /// <summary>
    /// 目标服务名
    /// 如果appurl配置的是网关地址，服务名必须与网关/服务发现中的服务名一致(ServiceDiscovery:ServiceName)
    /// </summary>
    public string ServiceName { get; }

    /// <summary>
    /// 处理器名
    /// </summary>
    public string HandlerName { get; }

    public DtmBranchAttribute(string serviceName, string handlerName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("serviceName 不能为空。", nameof(serviceName));
        }

        if (string.IsNullOrWhiteSpace(handlerName))
        {
            throw new ArgumentException("handlerName 不能为空。", nameof(handlerName));
        }

        ServiceName = serviceName.Trim();
        HandlerName = handlerName.Trim();
    }
}


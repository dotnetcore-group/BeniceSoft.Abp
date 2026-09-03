namespace BeniceSoft.Abp.ServiceDiscovery;

/// <summary>
/// 服务注册接口（业务服务使用）
/// 职责：业务服务向网关注册自己
/// </summary>
public interface IServiceRegistry
{
    /// <summary>
    /// 注册服务实例
    /// </summary>
    Task RegisterAsync(ServiceInstance instance);

    /// <summary>
    /// 注销服务实例
    /// </summary>
    Task DeregisterAsync(string serviceName, string address);
}


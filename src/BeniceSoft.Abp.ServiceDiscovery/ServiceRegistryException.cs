namespace BeniceSoft.Abp.ServiceDiscovery;

/// <summary>
/// 向网关注册或注销服务失败
/// </summary>
public class ServiceRegistryException : Exception
{
    public ServiceRegistryException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

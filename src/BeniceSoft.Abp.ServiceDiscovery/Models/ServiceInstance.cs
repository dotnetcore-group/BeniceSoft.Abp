namespace BeniceSoft.Abp.ServiceDiscovery;

/// <summary>
/// 服务实例
/// </summary>
public class ServiceInstance
{
    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// 服务地址，形如 http://10.0.0.1:6002
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// 元数据（版本、环境、权重、标签等）
    /// </summary>
    public ServiceMetadata Metadata { get; set; } = new();
}


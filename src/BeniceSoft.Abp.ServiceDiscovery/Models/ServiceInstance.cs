namespace BeniceSoft.Abp.ServiceDiscovery;

/// <summary>
/// 服务实例（用于服务注册的 DTO）
/// </summary>
public class ServiceInstance
{
    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// 实例 ID（唯一标识）
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 服务地址（http://host:port）
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// 元数据（版本、环境、权重、标签等）
    /// </summary>
    public ServiceMetadata Metadata { get; set; } = new();
}


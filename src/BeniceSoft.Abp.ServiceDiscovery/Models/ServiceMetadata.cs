namespace BeniceSoft.Abp.ServiceDiscovery;

/// <summary>
/// 服务元数据
/// </summary>
public class ServiceMetadata
{
    /// <summary>
    /// 应用版本号（用于标识应用的构建版本，如 1.0.0、1.0.1）
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// API 版本号（用于 Swagger 文档的版本标识，如 v1、v2）
    /// 只有在 API 有破坏性变更时才修改
    /// </summary>
    public string ApiVersion { get; set; } = string.Empty;

    /// <summary>
    /// 环境（Development、Staging、Production，应用服务启动会自动赋值）
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// 灰度标签
    /// </summary>
    public GrayTagEnum? GrayTag { get; set; }

    /// <summary>
    /// 自定义元数据
    /// </summary>
    public Dictionary<string, string> Custom { get; set; } = [];
}


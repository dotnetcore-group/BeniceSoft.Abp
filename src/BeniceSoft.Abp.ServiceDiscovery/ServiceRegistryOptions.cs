using System;

namespace BeniceSoft.Abp.ServiceDiscovery;

/// <summary>
/// 服务注册配置选项
/// </summary>
public class ServiceRegistryOptions
{
    /// <summary>
    /// 服务名称（必填）
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// 对外可访问地址（可选）。未配置时从监听地址里取第一个具体主机
    /// 示例：http://10.0.0.1:6002
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// 主机名（可选，用于 Address 自动构建，默认自动检测）
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    /// 端口（可选，用于 Address 自动构建，默认自动检测）
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// 是否启用 HTTPS（可选，用于 Address 自动构建，默认 false）
    /// </summary>
    public bool IsHttps { get; set; }

    /// <summary>
    /// 网关 Admin 内网地址（HTTP 服务注册/注销，如 http://gateway-admin:5188）
    /// </summary>
    public string GatewayBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 服务元数据（版本、环境、权重、标签等）
    /// </summary>
    public ServiceMetadata Metadata { get; set; } = new();

    /// <summary>
    /// 是否启用自动注册（默认 true）
    /// </summary>
    public bool EnableAutoRegistration { get; set; } = true;

    /// <summary>
    /// 注册失败时是否阻塞服务启动（默认 false）
    /// 设置为 false 时，注册失败不会影响服务启动，会在后台持续重试
    /// 设置为 true 时，注册失败会导致服务启动失败
    /// </summary>
    public bool BlockStartupOnRegistrationFailure { get; set; } = false;

    /// <summary>
    /// 注册重试间隔（默认 5 秒）
    /// </summary>
    public TimeSpan RegistrationRetryInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 启动时最大重试次数（仅当 BlockStartupOnRegistrationFailure = true 时有效，默认 3 次）
    /// </summary>
    public int MaxStartupRetries { get; set; } = 3;

    /// <summary>
    /// 注册成功后的心跳间隔（默认 10 秒）。再次调用注册接口刷新网关 LastHeartbeat。
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(10);
}


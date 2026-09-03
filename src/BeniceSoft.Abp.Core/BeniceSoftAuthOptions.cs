namespace BeniceSoft.Abp.Core;

/// <summary>
/// 认证与机器身份配置（配置节：Auth）
/// </summary>
public class BeniceSoftAuthOptions
{
    /// <summary>
    /// 权限中心地址（获取用户数据权限），配网关地址
    /// </summary>
    public string PermissionCenterUrl { get; set; } = string.Empty;

    /// <summary>
    /// AM 认证中心地址（JWT 校验 / 机器身份换 token），配网关地址
    /// </summary>
    public string? Authority { get; set; }

    public string? Audience { get; set; }

    /// <summary>
    /// 服务间 client_credentials 的 ClientId（可选）
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// 服务间 client_credentials 的 ClientSecret（可选）
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// 机器身份请求的 scope，默认 api
    /// </summary>
    public string Scope { get; set; } = "api";

    public bool HasMachineCredentials =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !string.IsNullOrWhiteSpace(Authority);

    public string ResolveTokenEndpoint()
    {
        if (string.IsNullOrWhiteSpace(Authority))
        {
            return string.Empty;
        }

        return $"{Authority.TrimEnd('/')}/connect/token";
    }
}

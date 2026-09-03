namespace BeniceSoft.Abp.Http.Client;

/// <summary>
/// 机器身份 access_token 提供器（client_credentials）。
/// 未配置或获取失败时返回 null，不抛异常。
/// </summary>
public interface IMachineAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

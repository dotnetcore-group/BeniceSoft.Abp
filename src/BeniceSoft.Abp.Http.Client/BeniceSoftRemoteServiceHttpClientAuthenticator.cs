using System.Net.Http.Headers;
using BeniceSoft.Core;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Http.Client.Authentication;

namespace BeniceSoft.Abp.Http.Client;

/// <summary>
/// ABP 远程调用鉴权：若 Factory 已透传用户 Bearer 则跳过；
/// 否则尝试机器身份；未配置或获取失败则保持空 Authorization。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IRemoteServiceHttpClientAuthenticator))]
public class BeniceSoftRemoteServiceHttpClientAuthenticator :
    IRemoteServiceHttpClientAuthenticator,
    ITransientDependency
{
    private readonly IMachineAccessTokenProvider _machineAccessTokenProvider;
    private readonly ILogger<BeniceSoftRemoteServiceHttpClientAuthenticator> _logger;

    public BeniceSoftRemoteServiceHttpClientAuthenticator(
        IMachineAccessTokenProvider machineAccessTokenProvider,
        ILogger<BeniceSoftRemoteServiceHttpClientAuthenticator> logger)
    {
        _machineAccessTokenProvider = machineAccessTokenProvider;
        _logger = logger;
    }

    public virtual async Task Authenticate(RemoteServiceHttpClientAuthenticateContext context)
    {
        if (HasAuthorization(context))
        {
            return;
        }

        var accessToken = await _machineAccessTokenProvider.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        context.Request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        _logger.LogInformation(
            "Applied machine credentials to remote call. RemoteService={RemoteServiceName}",
            context.RemoteServiceName);
    }

    private static bool HasAuthorization(RemoteServiceHttpClientAuthenticateContext context)
    {
        if (context.Request.Headers.Authorization != null)
        {
            return true;
        }

        if (context.Request.Headers.Contains(BeniceSoftHttpConstant.Authorization))
        {
            return true;
        }

        return context.Client.DefaultRequestHeaders.Contains(BeniceSoftHttpConstant.Authorization);
    }
}

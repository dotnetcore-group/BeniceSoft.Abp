using System.Reflection;
using System.Text.Json;
using BeniceSoft.Abp.Core.Users;
using BeniceSoft.Abp.Sample.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Http.Client.Proxying;
using Volo.Abp.MultiTenancy;

namespace BeniceSoft.Abp.Sample.Application.Services;

/// <summary>
/// 对齐 Warehouse FileExportHandlingJob：本地 Change(Tenant) 后远程调用，观察 __tenant / CurrentTenant。
/// </summary>
[AllowAnonymous]
public class RemoteTenantProbeAppService : SampleAppServiceBase, IRemoteTenantProbeAppService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ICurrentTenant _currentTenant;
    private readonly IBeniceSoftCurrentUser _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IProxyHttpClientFactory _proxyHttpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public RemoteTenantProbeAppService(
        ICurrentTenant currentTenant,
        IBeniceSoftCurrentUser currentUser,
        IHttpContextAccessor httpContextAccessor,
        IProxyHttpClientFactory proxyHttpClientFactory,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
        _proxyHttpClientFactory = proxyHttpClientFactory;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public Task<RemoteTenantEchoDto> EchoTenantAsync()
    {
        var tenantKey = TenantResolverConsts.DefaultTenantKey;
        string? headerTenant = null;
        if (_httpContextAccessor.HttpContext?.Request.Headers.TryGetValue(tenantKey, out var values) == true)
        {
            headerTenant = values.ToString();
        }

        return Task.FromResult(new RemoteTenantEchoDto
        {
            CurrentTenantId = _currentTenant.Id,
            CurrentTenantName = _currentTenant.Name,
            HeaderTenant = headerTenant,
            IsAuthenticated = _currentUser.IsAuthenticated,
            UserId = _currentUser.Id,
            Note = "接收端：对比 HeaderTenant 与 UseMultiTenancy 解析后的 CurrentTenantId。"
        });
    }

    public Task<RemoteTenantFactoryProbeDto> ProbeFactoryUnderTenantChangeAsync(Guid? tenantId)
    {
        // 模拟 FileExportHandlingJob：using (_currentTenant.Change(args.TenantId))
        using (_currentTenant.Change(tenantId))
        {
            return Task.FromResult(CaptureFactoryProbe("仅探测 Factory；尚未发起真实 HTTP。"));
        }
    }

    public async Task<RemoteTenantRoundTripDto> RoundTripUnderTenantChangeAsync(Guid? tenantId, string? echoBaseUrl = null)
    {
        var result = new RemoteTenantRoundTripDto();

        using (_currentTenant.Change(tenantId))
        {
            result.Outbound = CaptureFactoryProbe("Change 后 Create Proxy HttpClient，再请求本机 Echo。");

            var baseUrl = (echoBaseUrl ?? _configuration["RemoteTenantProbe:EchoBaseUrl"] ?? "http://localhost:6101")
                .TrimEnd('/');
            var url = $"{baseUrl}/api/sample/remote-tenant-probe/echo-tenant";
            result.EchoRequestUrl = url;

            try
            {
                // 与 ABP 代理相同：先走 IProxyHttpClientFactory.Create
                var proxyClient = _proxyHttpClientFactory.Create("Wecharmer.FileCenter");
                using var request = new HttpRequestMessage(HttpMethod.Post, url);

                // 复制 Factory 写在 DefaultRequestHeaders 上的头（含将来的 __tenant）
                foreach (var header in proxyClient.DefaultRequestHeaders)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                // 后台 Job 通常无用户 Bearer：此处不故意透传入站 Authorization，贴近机器调用
                using var sendClient = _httpClientFactory.CreateClient();
                using var response = await sendClient.SendAsync(request);
                result.EchoHttpStatus = (int)response.StatusCode;
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    result.Error = body;
                    return result;
                }

                // 兼容 ResponseResult 包装或裸 DTO
                result.Inbound = TryDeserializeEcho(body);
                if (result.Inbound is null)
                {
                    result.Error = "无法反序列化 Echo 响应: " + body;
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.ToString();
            }
        }

        return result;
    }

    private RemoteTenantFactoryProbeDto CaptureFactoryProbe(string note)
    {
        var factoryType = _proxyHttpClientFactory.GetType();
        var replace = factoryType.GetCustomAttribute<DependencyAttribute>()?.ReplaceServices == true;

        var client = _proxyHttpClientFactory.Create("Wecharmer.FileCenter");
        var tenantKey = TenantResolverConsts.DefaultTenantKey;
        var hasTenant = client.DefaultRequestHeaders.TryGetValues(tenantKey, out var values);
        var tenantValue = hasTenant ? string.Join(",", values!) : null;

        return new RemoteTenantFactoryProbeDto
        {
            AmbientTenantIdAfterChange = _currentTenant.Id,
            FactoryType = factoryType.AssemblyQualifiedName ?? factoryType.FullName ?? factoryType.Name,
            HasReplaceServicesAttribute = replace,
            OutboundHasTenantHeader = hasTenant,
            OutboundTenantHeaderValue = tenantValue,
            Note = note
        };
    }

    private static RemoteTenantEchoDto? TryDeserializeEcho(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // ResponseResult: { code, data: { ... } }
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                return JsonSerializer.Deserialize<RemoteTenantEchoDto>(data.GetRawText(), JsonOptions);
            }

            return JsonSerializer.Deserialize<RemoteTenantEchoDto>(body, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}

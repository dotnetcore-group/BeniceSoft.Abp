using System.Text.Json;
using System.Text.Json.Serialization;
using BeniceSoft.Abp.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Http.Client;

/// <summary>
/// 从 <see cref="BeniceSoftAuthOptions"/> 读取机器身份，按 client_credentials 换 token。
/// 未配置或获取失败时返回 null，不抛异常。
/// </summary>
[ExposeServices(typeof(IMachineAccessTokenProvider))]
public class MachineAccessTokenProvider : IMachineAccessTokenProvider, ISingletonDependency
{
    public const string HttpClientName = "BeniceSoft.MachineCredentials";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MachineAccessTokenProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public MachineAccessTokenProvider(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<MachineAccessTokenProvider> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var authOptions = _serviceProvider.GetService<BeniceSoftAuthOptions>();
        if (authOptions is null || !authOptions.HasMachineCredentials)
        {
            _logger.LogWarning(
                "Machine credentials skipped. AuthOptionsRegistered={Registered}, HasMachineCredentials={Configured}",
                authOptions is not null,
                authOptions?.HasMachineCredentials == true);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_cachedToken) && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _cachedToken;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedToken) && DateTimeOffset.UtcNow < _expiresAt)
            {
                return _cachedToken;
            }

            return await RequestTokenAsync(authOptions, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string?> RequestTokenAsync(
        BeniceSoftAuthOptions options,
        CancellationToken cancellationToken)
    {
        var endpoint = options.ResolveTokenEndpoint();
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = options.ClientId!,
                ["client_secret"] = options.ClientSecret!,
                ["scope"] = string.IsNullOrWhiteSpace(options.Scope) ? "api" : options.Scope
            });

            using var response = await client.PostAsync(endpoint, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Machine credentials token request failed. Endpoint={Endpoint}, Status={StatusCode}, Body={Body}",
                    endpoint,
                    (int)response.StatusCode,
                    body);
                return null;
            }

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(body, JsonOptions);
            if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                _logger.LogWarning(
                    "Machine credentials token response missing access_token. Endpoint={Endpoint}",
                    endpoint);
                return null;
            }

            var lifetimeSeconds = tokenResponse.ExpiresIn > 0 ? tokenResponse.ExpiresIn : 3600;
            var refreshSkewSeconds = Math.Min(60, Math.Max(5, lifetimeSeconds / 10));
            _cachedToken = tokenResponse.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(lifetimeSeconds - refreshSkewSeconds);

            _logger.LogDebug(
                "Machine credentials access token acquired. ClientId={ClientId}, ExpiresIn={ExpiresIn}s",
                options.ClientId,
                lifetimeSeconds);

            return _cachedToken;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Machine credentials token request threw. Endpoint={Endpoint}, ClientId={ClientId}",
                endpoint,
                options.ClientId);
            return null;
        }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "Bearer";
    }
}

using BeniceSoft.Abp.Auth.Core;
using BeniceSoft.Abp.Core;
using BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Auth.Authentication;

/// <summary>
/// 从 <see cref="BeniceSoftAuthOptions"/> 读取机器身份，按 client_credentials 换 token
/// 未配置或获取失败时返回 null，不抛异常
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

        var endpoint = authOptions.ResolveTokenEndpoint();
        try
        {
            var scope = string.Join(" ", (string.IsNullOrWhiteSpace(authOptions.Scope) ? "api" : authOptions.Scope)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
            // 凭据参与摘要，避免轮换后继续复用旧凭据取得的 Token
            var keyContext = JsonSerializer.Serialize(new[]
            {
                new Uri(endpoint, UriKind.Absolute).AbsoluteUri,
                authOptions.ClientId,
                scope,
                authOptions.ClientSecret
            });
            var cacheKey = $"BeniceSoft:Auth:MachineToken:v1:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyContext)))}";
            var cache = _serviceProvider.GetRequiredService<IDistributedCache>();
            if (cache is MemoryDistributedCache)
            {
                _logger.LogWarning("Machine credentials require a shared distributed cache. ClientId={ClientId}",
                    authOptions.ClientId);
                return null;
            }

            var cachedToken = await cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(cachedToken))
            {
                return cachedToken;
            }

            var lockProvider = _serviceProvider.GetRequiredService<IDistributedLockProvider>();
            await using var handle = await lockProvider.TryAcquireAsync(
                $"{cacheKey}:refresh", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30),
                TimeSpan.FromMilliseconds(100), autoRenew: true, cancellationToken: cancellationToken);
            if (handle is null)
            {
                _logger.LogWarning("Machine credentials token refresh lock unavailable. ClientId={ClientId}",
                    authOptions.ClientId);
                return null;
            }

            cachedToken = await cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(cachedToken))
            {
                return cachedToken;
            }

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = authOptions.ClientId!,
                ["client_secret"] = authOptions.ClientSecret!,
                ["scope"] = scope
            });

            var requestedAt = DateTimeOffset.UtcNow;
            using var response = await client.PostAsync(endpoint, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Machine credentials token request failed. Endpoint={Endpoint}, Status={StatusCode}",
                    endpoint,
                    (int)response.StatusCode);
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

            if (tokenResponse.ExpiresIn <= 0 ||
                requestedAt.AddSeconds(tokenResponse.ExpiresIn) <= DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("Machine credentials token response has no remaining lifetime. Endpoint={Endpoint}",
                    endpoint);
                return null;
            }

            if (!await handle.RenewAsync(TimeSpan.FromMinutes(1)))
            {
                _logger.LogWarning("Machine credentials token refresh lock lost. ClientId={ClientId}",
                    authOptions.ClientId);
                return null;
            }

            var refreshSkewSeconds = Math.Min(60, Math.Max(1, tokenResponse.ExpiresIn / 10));
            var refreshAt = requestedAt.AddSeconds(tokenResponse.ExpiresIn - refreshSkewSeconds);
            if (refreshAt <= DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("Machine credentials token response has insufficient remaining lifetime. Endpoint={Endpoint}",
                    endpoint);
                return null;
            }

            await cache.SetStringAsync(cacheKey, tokenResponse.AccessToken,
                new DistributedCacheEntryOptions { AbsoluteExpiration = refreshAt }, cancellationToken);
            if (refreshAt <= DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("Machine credentials token lifetime elapsed while storing the shared cache. ClientId={ClientId}",
                    authOptions.ClientId);
                return null;
            }

            _logger.LogDebug(
                "Machine credentials access token acquired. ClientId={ClientId}, ExpiresIn={ExpiresIn}s",
                authOptions.ClientId,
                tokenResponse.ExpiresIn);

            return tokenResponse.AccessToken;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Machine credentials token request threw. Endpoint={Endpoint}, ClientId={ClientId}",
                endpoint,
                authOptions.ClientId);
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

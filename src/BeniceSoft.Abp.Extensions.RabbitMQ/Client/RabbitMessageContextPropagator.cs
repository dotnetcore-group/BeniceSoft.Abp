using System.Security.Claims;
using BeniceSoft.Abp.Core.Messaging;
using BeniceSoft.Abp.Core.Users;
using BeniceSoft.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;

namespace BeniceSoft.Abp.Extensions.RabbitMQ;

/// <summary>
/// 发布时写入、消费时还原当前用户 / 租户（对齐 DTM EventBus Header 约定）。
/// </summary>
public sealed class RabbitMessageContextPropagator
{
    public const string AuthenticationType = "RabbitMqWork";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMessageContextPropagator> _logger;

    public RabbitMessageContextPropagator(
        IServiceProvider serviceProvider,
        ILogger<RabbitMessageContextPropagator> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void Attach(BasicProperties properties)
    {
        properties.Headers ??= new Dictionary<string, object?>();

        if (!properties.Headers.ContainsKey(MessageContextHeaderNames.UserClaims))
        {
            var userClaims = ResolveUserClaimsHeader();
            if (!userClaims.IsNull())
            {
                properties.Headers[MessageContextHeaderNames.UserClaims] = userClaims;
            }
        }

        if (!properties.Headers.ContainsKey(MessageContextHeaderNames.TenantId))
        {
            var tenantId = ResolveTenantIdHeader();
            if (!tenantId.IsNull())
            {
                properties.Headers[MessageContextHeaderNames.TenantId] = tenantId;
            }
        }
    }

    public IDisposable Restore(IServiceProvider scopeServiceProvider, IReadOnlyBasicProperties? properties)
    {
        var principalAccessor = scopeServiceProvider.GetService<ICurrentPrincipalAccessor>();
        var currentTenant = scopeServiceProvider.GetService<ICurrentTenant>();

        IDisposable? principalChange = null;
        IDisposable? tenantChange = null;

        try
        {
            var principal = BuildPrincipal(properties?.Headers);
            if (principal != null && principalAccessor != null)
            {
                principalChange = principalAccessor.Change(principal);
            }

            var tenantId = ReadTenantId(properties?.Headers);
            if (currentTenant != null)
            {
                tenantChange = currentTenant.Change(tenantId);
            }
        }
        catch (Exception ex)
        {
            principalChange?.Dispose();
            tenantChange?.Dispose();
            _logger.LogWarning(ex, "Failed to restore user/tenant from RabbitMQ headers");
            return NullDisposable.Instance;
        }

        return new CombinedDisposable(principalChange, tenantChange);
    }

    private string? ResolveUserClaimsHeader()
    {
        var httpContextAccessor = _serviceProvider.GetService<IHttpContextAccessor>();
        if (httpContextAccessor?.HttpContext != null &&
            httpContextAccessor.HttpContext.Request.Headers.TryGetValue(MessageContextHeaderNames.UserClaims, out var value) &&
            !string.IsNullOrWhiteSpace(value.ToStringSafe()))
        {
            return value.ToStringSafe();
        }

        var currentUser = _serviceProvider.GetService<IBeniceSoftCurrentUser>();
        if (currentUser is { IsAuthenticated: true })
        {
            var claims = currentUser.GetAllClaims();
            if (claims.Length > 0)
            {
                return MessageContextTransfer.EncodeClaims(claims);
            }
        }

        var principalAccessor = _serviceProvider.GetService<ICurrentPrincipalAccessor>();
        var principalClaims = principalAccessor?.Principal?.Claims.ToArray();
        if (principalClaims is { Length: > 0 })
        {
            return MessageContextTransfer.EncodeClaims(principalClaims);
        }

        return null;
    }

    private string? ResolveTenantIdHeader()
    {
        var httpContextAccessor = _serviceProvider.GetService<IHttpContextAccessor>();
        if (httpContextAccessor?.HttpContext != null &&
            httpContextAccessor.HttpContext.Request.Headers.TryGetValue(MessageContextHeaderNames.TenantId, out var tenantId) &&
            !string.IsNullOrWhiteSpace(tenantId.ToStringSafe()))
        {
            return tenantId.ToStringSafe();
        }

        var currentTenant = _serviceProvider.GetService<ICurrentTenant>();
        return currentTenant?.Id?.ToString();
    }

    private ClaimsPrincipal? BuildPrincipal(IDictionary<string, object?>? headers)
    {
        if (headers == null ||
            !headers.TryGetValue(MessageContextHeaderNames.UserClaims, out var userClaimsRaw))
        {
            return null;
        }

        var encoded = MessageContextTransfer.ReadHeaderString(userClaimsRaw);
        try
        {
            return MessageContextTransfer.DecodePrincipal(encoded, AuthenticationType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decode RabbitMQ user claims header");
            return null;
        }
    }

    private static Guid? ReadTenantId(IDictionary<string, object?>? headers)
    {
        if (headers == null ||
            !headers.TryGetValue(MessageContextHeaderNames.TenantId, out var tenantRaw))
        {
            return null;
        }

        var guid = MessageContextTransfer.ReadHeaderString(tenantRaw).ToGuid();
        return guid == Guid.Empty ? null : guid;
    }

    private sealed class CombinedDisposable(IDisposable? first, IDisposable? second) : IDisposable
    {
        public void Dispose()
        {
            first?.Dispose();
            second?.Dispose();
        }
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}

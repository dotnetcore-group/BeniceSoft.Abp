using BeniceSoft.Abp.Core.Users;
using BeniceSoft.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Security.Claims;
using System.Text;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.Local;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.RabbitMQ;
using Volo.Abp.Security.Claims;
using Volo.Abp.Timing;
using Volo.Abp.Tracing;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

[Dependency(ReplaceServices = true)]
[ExposeServices([
    typeof(IDistributedEventBus),
    typeof(IRabbitMqDistributedEventBus),
    typeof(RabbitMqDistributedEventBus),
    typeof(BeniceSoftRabbitMqDistributedEventBus)])]
public class BeniceSoftRabbitMqDistributedEventBus : RabbitMqDistributedEventBus
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly ICurrentTenant _currentTenant;
    private readonly IBeniceSoftCurrentUser _currentUser;
    private readonly ILogger<BeniceSoftRabbitMqDistributedEventBus> _logger;
    private int _initialized;

    public BeniceSoftRabbitMqDistributedEventBus(
        IOptions<AbpRabbitMqEventBusOptions> options,
        IConnectionPool connectionPool, IRabbitMqSerializer serializer,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<AbpDistributedEventBusOptions> distributedEventBusOptions,
        IRabbitMqMessageConsumerFactory messageConsumerFactory,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IGuidGenerator guidGenerator,
        IClock clock,
        IEventHandlerInvoker eventHandlerInvoker,
        ILocalEventBus localEventBus,
        ICorrelationIdProvider correlationIdProvider,
        IHttpContextAccessor httpContextAccessor,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IBeniceSoftCurrentUser currentUser,
        ILogger<BeniceSoftRabbitMqDistributedEventBus> logger)
        : base(options, connectionPool, serializer, serviceScopeFactory, distributedEventBusOptions, messageConsumerFactory, currentTenant, unitOfWorkManager, guidGenerator, clock, eventHandlerInvoker, localEventBus, correlationIdProvider)
    {
        _currentTenant = currentTenant;
        _httpContextAccessor = httpContextAccessor;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _currentUser = currentUser;
        _logger = logger;
    }

    public override async Task PublishManyFromOutboxAsync(IEnumerable<OutgoingEventInfo> outgoingEvents, OutboxConfig outboxConfig)
    {
        using var channel = await (await ConnectionPool.GetAsync(AbpRabbitMqEventBusOptions.ConnectionName))
            .CreateChannelAsync(new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true,
                new ThrottlingRateLimiter(256)));

        var outgoingEventArray = outgoingEvents.ToArray();
        foreach (var outgoingEvent in outgoingEventArray)
        {
            Dictionary<string, object> headers = new();

            if (_httpContextAccessor.HttpContext != null &&
                _httpContextAccessor.HttpContext.Request.Headers.TryGetValue(DtmRequestHeaderNames.UserClaims, out var value) &&
                !string.IsNullOrWhiteSpace(value.ToStringSafe()))
            {
                headers[DtmRequestHeaderNames.UserClaims] = value.ToStringSafe();
            }
            else if (_currentUser.IsAuthenticated)
            {
                var claims = _currentUser.GetAllClaims()
                    .Select(x => new ClaimTransferItem(x.Type, x.Value, x.ValueType, x.Issuer, x.OriginalIssuer))
                    .ToList();
                if (claims.Count > 0)
                {
                    headers[DtmRequestHeaderNames.UserClaims] = StringUtils.Hex36String(JsonUtils.SerializeBytes(claims));
                }
            }

            if (_httpContextAccessor.HttpContext != null &&
                _httpContextAccessor.HttpContext.Request.Headers.TryGetValue(DtmRequestHeaderNames.TenantId, out var tenantId) &&
                !string.IsNullOrWhiteSpace(tenantId.ToStringSafe()))
            {
                headers[DtmRequestHeaderNames.TenantId] = tenantId.ToStringSafe();
            }
            else if (_currentTenant.Id.HasValue)
            {
                headers[DtmRequestHeaderNames.TenantId] = _currentTenant.Id.Value.ToString();
            }

            _logger.LogInformation("publish message headers={@Headers}, EventName={EventName}", headers, outgoingEvent.EventName);

            await PublishAsync(
                channel,
                outgoingEvent.EventName,
                outgoingEvent.EventData,
                headersArguments: headers,
                eventId: outgoingEvent.Id);
        }
    }

    /// <summary>
    /// 初始化
    /// </summary>
    public override void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        base.Initialize();
        Consumer.OnMessageReceived(ProcessEventAppendHeadersAsync);
    }

    internal async Task ProcessEventAppendHeadersAsync(IChannel channel, BasicDeliverEventArgs ea)
    {
        var eventName = ea.RoutingKey;
        var eventType = EventTypes.GetOrDefault(eventName);
        if (eventType == null)
        {
            return;
        }

        var eventBytes = ea.Body.ToArray();
        var eventData = Serializer.Deserialize(eventBytes, eventType);

        var principal = BuildPrincipalFromHeaders(ea.BasicProperties?.Headers, eventName);

        Guid? tenantId = null;
        if (ea.BasicProperties?.Headers != null &&
            ea.BasicProperties.Headers.TryGetValue(DtmRequestHeaderNames.TenantId, out var tenantRaw))
        {
            tenantId = ReadHeaderString(tenantRaw).ToGuid();
        }

        if (eventData is IEventDataMayHaveTenantId eventDataMayHaveTenantId &&
            !eventDataMayHaveTenantId.IsMultiTenant(out tenantId))
        {
            tenantId = null;
        }

        using var principalChange = principal is null ? null : _currentPrincipalAccessor.Change(principal);
        using var tenantChange = _currentTenant.Change(tenantId);

        var correlationId = CorrelationIdProvider.Get();
        var messageId = ea.BasicProperties?.MessageId ?? Guid.NewGuid().ToString("N");

        if (await AddToInboxAsync(messageId, eventName, eventType, eventData!, correlationId))
        {
            return;
        }

        await TriggerHandlersAsync(eventType, eventData);
    }

    private static string ReadHeaderString(object? headerValue)
    {
        return headerValue switch
        {
            null => string.Empty,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
            string text => text,
            _ => headerValue.ToStringSafe()
        };
    }

    private ClaimsPrincipal? BuildPrincipalFromHeaders(IDictionary<string, object?>? headers, string eventName)
    {
        if (headers == null ||
            !headers.TryGetValue(DtmRequestHeaderNames.UserClaims, out var userClaimsRaw))
        {
            return null;
        }

        var userClaims = ReadHeaderString(userClaimsRaw);
        if (string.IsNullOrWhiteSpace(userClaims))
        {
            return null;
        }

        try
        {
            var claimsBytes = StringUtils.Hex36Bytes(userClaims);
            var claimItems = JsonUtils.DeserializeBytes<List<ClaimTransferItem>>(claimsBytes) ?? [];
            var claims = claimItems
                .Select(x => new Claim(x.Type, x.Value, x.ValueType, x.Issuer, x.OriginalIssuer))
                .ToList();

            return claims.Count == 0
                ? null
                : new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "RabbitMqEvent"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ consumer restore user claims failed. Event={EventName}", eventName);
            return null;
        }
    }

    private sealed record ClaimTransferItem(string Type, string Value, string ValueType, string Issuer, string OriginalIssuer);
}

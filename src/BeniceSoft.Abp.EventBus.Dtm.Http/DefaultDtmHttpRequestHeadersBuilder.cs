using BeniceSoft.Abp.Core.Users;
using BeniceSoft.Core;
using Microsoft.Extensions.Options;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

/// <summary>
/// 默认的 DTM Http请求头构建器，从当前上下文中补齐通用请求头
/// </summary>
public class DefaultDtmHttpRequestHeadersBuilder : IDtmRequestHeadersBuilder, ITransientDependency
{
    private readonly IBeniceSoftCurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly DtmHttpOptions _dtmHttpOptions;
    private readonly DtmTransactionDbContextOptions _dtmDbContextOptions;
    private readonly IConnectionStringResolver _connectionStringResolver;
    private readonly IConnectionStringHasher _connectionStringHasher;

    public DefaultDtmHttpRequestHeadersBuilder(
        IBeniceSoftCurrentUser currentUser,
        ICurrentTenant currentTenant,
        IConnectionStringResolver connectionStringResolver,
        IConnectionStringHasher connectionStringHasher,
        IOptions<DtmHttpOptions> dtmHttpOptions,
        IOptions<DtmTransactionDbContextOptions> dtmDbContextOptions)
    {
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _connectionStringResolver = connectionStringResolver;
        _connectionStringHasher = connectionStringHasher;
        _dtmHttpOptions = dtmHttpOptions.Value;
        _dtmDbContextOptions = dtmDbContextOptions.Value;
    }

    public async Task BuildHeadersAsync(IDictionary<string, string> headers)
    {
        if (!headers.ContainsKey(DtmRequestHeaderNames.ActionApiToken) && !string.IsNullOrWhiteSpace(_dtmHttpOptions.ActionApiToken))
        {
            headers[DtmRequestHeaderNames.ActionApiToken] = _dtmHttpOptions.ActionApiToken;
        }

        if (!headers.ContainsKey(DtmRequestHeaderNames.TenantId) && _currentTenant.Id.HasValue)
        {
            headers[DtmRequestHeaderNames.TenantId] = _currentTenant.Id.Value.ToString();
        }

        await TryAppendDbContextHeadersAsync(headers);

        if (_currentUser.IsAuthenticated)
        {
            var claims = _currentUser.GetAllClaims()
                .Select(x => new ClaimTransferItem(x.Type, x.Value, x.ValueType, x.Issuer, x.OriginalIssuer))
                .ToList();

            if (claims.Count > 0)
            {
                headers[DtmRequestHeaderNames.UserClaims] = StringUtils.Hex36String(JsonUtils.SerializeBytes(claims));
            }
        }
    }

    protected virtual async Task TryAppendDbContextHeadersAsync(IDictionary<string, string> headers)
    {
        if (headers.ContainsKey(DtmRequestHeaderNames.DbContextType) && headers.ContainsKey(DtmRequestHeaderNames.HashedConnectionString))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_dtmDbContextOptions.DefaultDbContextTypeName))
        {
            return;
        }

        var dbContextType = Type.GetType(_dtmDbContextOptions.DefaultDbContextTypeName, throwOnError: false);
        if (dbContextType is null)
        {
            return;
        }

        if (!headers.ContainsKey(DtmRequestHeaderNames.DbContextType))
        {
            headers[DtmRequestHeaderNames.DbContextType] =
                $"{dbContextType.FullName}, {dbContextType.Assembly.GetName().Name}";
        }

        if (!headers.ContainsKey(DtmRequestHeaderNames.HashedConnectionString))
        {
            var connectionString = await _connectionStringResolver.ResolveAsync(dbContextType);
            headers[DtmRequestHeaderNames.HashedConnectionString] =
                await _connectionStringHasher.HashAsync(connectionString);
        }
    }

    private sealed record ClaimTransferItem(string Type, string Value, string ValueType, string Issuer, string OriginalIssuer);
}
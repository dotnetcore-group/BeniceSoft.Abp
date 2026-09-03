using System.Net.Http.Json;
using System.Text.Json;
using Dtmcli;
using Dtmcli.DtmImp;
using DtmCommon;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IDtmClient))]
public class AbpDtmHttpClient : IDtmClient, ISingletonDependency
{
    protected DtmHttpOptions HttpOptions { get; }

    protected DtmEventBoxesOptions BoxesOptions { get; }

    private readonly IHttpClientFactory _httpClientFactory;

    public AbpDtmHttpClient(
        IOptions<DtmHttpOptions> httpOptions,
        IOptions<DtmEventBoxesOptions> boxesOptions,
        IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        HttpOptions = httpOptions.Value;
        BoxesOptions = boxesOptions.Value;
    }

    public async Task<string> GenGid(CancellationToken cancellationToken)
    {
        using var response = await _httpClientFactory.CreateClient("dtmClient")
            .GetAsync("/api/dtmsvr/newGid", cancellationToken)
            .ConfigureAwait(false);

        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Utils.CheckStatus(response.StatusCode, content);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("DTM newGid 响应为空。");
        }

        var trimmed = content.Trim();
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.TryGetProperty("gid", out var gidElement) && gidElement.ValueKind == JsonValueKind.String)
            {
                return gidElement.GetString() ?? throw new InvalidOperationException("DTM newGid 响应 gid 为空。");
            }

            if (doc.RootElement.TryGetProperty("Gid", out gidElement) && gidElement.ValueKind == JsonValueKind.String)
            {
                return gidElement.GetString() ?? throw new InvalidOperationException("DTM newGid 响应 Gid 为空。");
            }
        }

        return trimmed.Trim('"');
    }

    public async Task TransCallDtm(TransBase tb, object body, string operation, CancellationToken cancellationToken)
    {
        string requestUri = "/api/dtmsvr/" + operation;
        using var response = await _httpClientFactory.CreateClient("dtmClient")
            .PostAsJsonAsync(requestUri, tb, cancellationToken).ConfigureAwait(false);
        Utils.CheckStatus(response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
    }

    public Task TransRegisterBranch(TransBase tb, Dictionary<string, string> added, string operation, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("AbpDtmHttpClient 暂未启用 TransRegisterBranch");
    }

    public Task<HttpResponseMessage> TransRequestBranch(TransBase tb, HttpMethod method, object body, string branchID, string op, string url,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("AbpDtmHttpClient 暂未启用 TransRequestBranch");
    }

    public TransBase TransBaseFromQuery(IQueryCollection query)
    {
        var gid = query.TryGetValue("gid", out var gidValues) ? gidValues.ToString() : string.Empty;
        var transType = query.TryGetValue("trans_type", out var transTypeValues) ? transTypeValues.ToString() : string.Empty;
        var branchId = query.TryGetValue("branch_id", out var branchIdValues) ? branchIdValues.ToString() : string.Empty;
        var op = query.TryGetValue("op", out var opValues) ? opValues.ToString() : string.Empty;

        return TransBase.NewTransBase(gid, transType, branchId, op);
    }
}

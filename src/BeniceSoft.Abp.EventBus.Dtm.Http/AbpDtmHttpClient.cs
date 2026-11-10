using System.Net.Http.Json;
using System.Text.Json;
using Dtmcli;
using Dtmcli.DtmImp;
using DtmCommon;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
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
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
            .PostAsJsonAsync(requestUri, body, SerializerOptions, cancellationToken).ConfigureAwait(false);
        Utils.CheckStatus(response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
    }

    public Task TransRegisterBranch(TransBase tb, Dictionary<string, string> added, string operation, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, string>
        {
            ["gid"] = tb.Gid,
            ["trans_type"] = tb.TransType
        };

        foreach (var pair in added)
        {
            body[pair.Key] = pair.Value;
        }

        return TransCallDtm(tb, body, operation, cancellationToken);
    }

    public async Task<HttpResponseMessage> TransRequestBranch(TransBase tb, HttpMethod method, object body, string branchID, string op, string url,
        CancellationToken cancellationToken)
    {
        var requestUrl = QueryHelpers.AddQueryString(url, new Dictionary<string, string?>
        {
            ["dtm"] = HttpOptions.DtmUrl.TrimEnd('/') + "/api/dtmsvr/",
            ["gid"] = tb.Gid,
            ["trans_type"] = tb.TransType,
            ["branch_id"] = branchID,
            ["op"] = op
        });

        using var request = new HttpRequestMessage(method, requestUrl);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: SerializerOptions);
        }

        if (tb.BranchHeaders is not null)
        {
            foreach (var pair in tb.BranchHeaders)
            {
                if (!request.Headers.TryAddWithoutValidation(pair.Key, pair.Value))
                {
                    throw new InvalidOperationException($"DTM 分支请求头不合法: {pair.Key}");
                }
            }
        }

        return await _httpClientFactory.CreateClient("branchClient")
            .SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public TransBase TransBaseFromQuery(IQueryCollection query)
    {
        var gid = query.TryGetValue("gid", out var gidValues) ? gidValues.ToString() : string.Empty;
        var transType = query.TryGetValue("trans_type", out var transTypeValues) ? transTypeValues.ToString() : string.Empty;
        var branchId = query.TryGetValue("branch_id", out var branchIdValues) ? branchIdValues.ToString() : string.Empty;
        var op = query.TryGetValue("op", out var opValues) ? opValues.ToString() : string.Empty;
        var dtm = query.TryGetValue("dtm", out var dtmValues) ? dtmValues.ToString() : string.Empty;

        var transaction = TransBase.NewTransBase(gid, transType, dtm, branchId);
        transaction.Op = op;
        return transaction;
    }
}

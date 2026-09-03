using BeniceSoft.Core;
using Dtmcli;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.EventBus.Distributed;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public class DtmMsgInfoModel : IDtmMsgInfoModel
{
    public bool EventsPublishingActionAdded { get; private set; }

    public string Gid { get; set; }

    public object DtmMessage { get; set; }

    [NotNull]
    public DbConnectionLookupInfoModel DbConnectionLookupInfo { get; set; }

    public List<OutgoingEventInfo> EventInfos { get; set; } = [];

    private readonly IDtmRequestHeadersBuilder _dtmRequestHeadersBuilder;

    public DtmMsgInfoModel(
        string gid,
        object dtmMessage,
        [NotNull] DbConnectionLookupInfoModel dbConnectionLookupInfo,
        IDtmRequestHeadersBuilder dtmRequestHeadersBuilder)
    {
        Gid = gid;
        DtmMessage = dtmMessage;
        DbConnectionLookupInfo = dbConnectionLookupInfo;
        _dtmRequestHeadersBuilder = dtmRequestHeadersBuilder;
    }

    internal async Task AddEventsPublishingActionAsync(DtmHttpOptions abpDtmEventBoxesOptions)
    {
        if (EventsPublishingActionAdded)
        {
            throw new AbpException("Duplicate events publishing action.");
        }

        var msg = (DtmMessage as Msg)!;

        // post data
        msg.Add(abpDtmEventBoxesOptions.GetPublishEventsAddress(), new DtmMsgPublishEventsRequest
        {
            OutgoingEventInfoListToByteString = StringUtils.Hex36String(JsonUtils.SerializeBytes(EventInfos))
        });

        // set headers
        var headers = new Dictionary<string, string>
        {
            {DtmRequestHeaderNames.ActionApiToken, abpDtmEventBoxesOptions.ActionApiToken},
            {
                DtmRequestHeaderNames.DbContextType,
                $"{DbConnectionLookupInfo.DbContextType.FullName}, {DbConnectionLookupInfo.DbContextType.Assembly.GetName().Name}"
            },
            {DtmRequestHeaderNames.TenantId, DbConnectionLookupInfo.TenantId?.ToStringSafe()!},
            {DtmRequestHeaderNames.HashedConnectionString, DbConnectionLookupInfo.HashedConnectionString},
        };
        if (_dtmRequestHeadersBuilder != null)
        {
            await _dtmRequestHeadersBuilder.BuildHeadersAsync(headers);
        }

        msg.SetBranchHeaders(headers);
        EventsPublishingActionAdded = true;
    }
}

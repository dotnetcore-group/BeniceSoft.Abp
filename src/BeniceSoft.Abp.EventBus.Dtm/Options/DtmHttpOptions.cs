namespace BeniceSoft.Abp.EventBus.Dtm;

public class DtmHttpOptions
{
    private string _appUrl = string.Empty;
    private string _publishEventsPath = "/dtm_boxes.DtmHttpService/PublishEvents";
    private string _queryPreparedPath = "/dtm_boxes.DtmHttpService/QueryPrepared";

    /// <summary>
    /// Use this token to invoke action APIs.
    /// </summary>
    /// <returns></returns>
    public string ActionApiToken { get; set; } = string.Empty;

    public string AppUrl
    {
        get => _appUrl;
        set => _appUrl = value.RemovePostFix("/");
    }

    /// <summary>
    /// Dtm 服务地址
    /// </summary>
    public string DtmUrl { get; set; } = string.Empty;

    /// <summary>
    /// 请求 Dtm 服务超时时间（毫秒），默认 30s。
    /// </summary>
    public int Timeout { get; set; } = 30 * 1000;

    /// <summary>
    /// Msg 事务超时时间（毫秒）。小于等于 0 表示使用 DTM 默认值。
    /// </summary>
    public int MessageTimeoutToFail { get; set; }

    /// <summary>
    /// Msg 重试间隔（毫秒）。小于等于 0 表示使用 DTM 默认值。
    /// </summary>
    public int MessageRetryInterval { get; set; }

    /// <summary>
    /// Msg 最大重试次数。小于等于 0 表示使用 DTM 默认值。
    /// </summary>
    public int MessageRetryLimit { get; set; }

    /// <summary>
    /// PublishEvents 幂等 Gid 缓存秒数，默认 10 分钟。
    /// </summary>
    public int ProcessedGidCacheSeconds { get; set; } = 10 * 60;

    public string PublishEventsPath
    {
        get => _publishEventsPath;
        set => _publishEventsPath = value.EnsureStartsWith('/');
    }

    public string QueryPreparedPath
    {
        get => _queryPreparedPath;
        set => _queryPreparedPath = value.EnsureStartsWith('/');
    }

    public string GetPublishEventsAddress()
    {
        return $"{AppUrl}{PublishEventsPath}";
    }

    public string GetQueryPreparedAddress()
    {
        return $"{AppUrl}{QueryPreparedPath}";
    }
}

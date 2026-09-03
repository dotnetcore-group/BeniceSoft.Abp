namespace BeniceSoft.Abp.EventBus.Dtm.Http;

/// <summary>
/// 全局事务执行选项（TCC/SAGA）。
/// </summary>
public class DtmGlobalTransactionOptions
{
    /// <summary>
    /// 是否等待事务结果。
    /// </summary>
    public bool EnableWaitResult { get; set; } = true;

    /// <summary>
    /// 超时时间（秒），小于等于 0 表示不设置。
    /// </summary>
    public int TimeoutToFail { get; set; }

    /// <summary>
    /// 重试间隔（秒），小于等于 0 表示不设置。
    /// </summary>
    public int RetryInterval { get; set; }

    /// <summary>
    /// 最大重试次数，小于等于 0 表示不设置。
    /// </summary>
    public int RetryLimit { get; set; }

    /// <summary>
    /// 全局分支请求头（会叠加到默认头）。
    /// </summary>
    public Dictionary<string, string> BranchHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);
}
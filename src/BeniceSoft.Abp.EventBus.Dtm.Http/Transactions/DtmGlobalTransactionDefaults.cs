namespace BeniceSoft.Abp.EventBus.Dtm.Http;

/// <summary>
/// TCC/SAGA 全局默认配置
/// </summary>
public class DtmGlobalTransactionDefaults
{
    public bool EnableWaitResult { get; set; } = true;

    /// <summary>
    /// 超时时间（秒），默认 60s，小于等于 0 表示不设置
    /// </summary>
    public int TimeoutToFail { get; set; } = 60;

    /// <summary>
    /// 重试间隔（秒），默认 5s，小于等于 0 表示不设置
    /// </summary>
    public int RetryInterval { get; set; } = 5;

    /// <summary>
    /// 最大重试次数，默认 3次，小于等于 0 表示不设置
    /// </summary>
    public int RetryLimit { get; set; } = 3;
}
namespace BeniceSoft.Abp.EventBus.Dtm;

public class DtmEventBoxesOptions
{
    /// <summary>
    /// 屏障表名
    /// SQL Server -> dtm.Barrier<br />
    /// MySQL -> dtm_barrier<br />
    /// PostgreSQL -> dtm.barrier<br />
    /// MongoDB -> dtm_barrier
    /// </summary>
    public string BarrierTableName { get; set; } = string.Empty;

    /// <summary>
    /// dtm 服务请求超时时间（毫秒）
    /// 默认 10000 毫秒（10 秒）
    /// </summary>
    public int DtmTimeout { get; set; } = 10 * 1000;
}

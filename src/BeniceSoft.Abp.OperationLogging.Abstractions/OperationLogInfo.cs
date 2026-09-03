namespace BeniceSoft.Abp.OperationLogging.Abstractions;

/// <summary>
/// 操作日志信息
/// </summary>
[Serializable]
public class OperationLogInfo
{
    /// <summary>
    /// 操作时间
    /// </summary>
    public DateTimeOffset OperationTime { get; set; }

    /// <summary>
    /// 系统名称
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// 业务模块
    /// </summary>
    public string BizModule { get; set; } = string.Empty;

    /// <summary>
    /// 操作类型
    /// </summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// 业务id
    /// </summary>
    public string BizId { get; set; } = string.Empty;

    /// <summary>
    /// 业务编码
    /// </summary>
    public string BizCode { get; set; } = string.Empty;

    /// <summary>
    /// 操作人id
    /// </summary>
    public long? OperatorId { get; set; }

    /// <summary>
    /// 操作人名称
    /// </summary>
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>
    /// 备注
    /// </summary>
    public string Remark { get; set; } = string.Empty;

    /// <summary>
    /// 扩展数据
    /// </summary>
    public Dictionary<string, object>? ExtraData { get; set; }
}
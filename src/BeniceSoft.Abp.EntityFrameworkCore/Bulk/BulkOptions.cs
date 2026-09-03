namespace BeniceSoft.Abp.EntityFrameworkCore.Bulk;

/// <summary>
/// 批量写通用选项
/// SqlServer 专有项（如 SqlBulkCopyOptions、禁用非聚集索引）挂在具体 Atom 上。
/// </summary>
public class BulkOptions
{
    /// <summary>目标表 Schema</summary>
    public string? Schema { get; set; }

    /// <summary>目标表名（构造 Atom 时从 EF 模型填充）</summary>
    public string? TableName { get; set; }

    /// <summary>后续 MERGE / UPDATE / DELETE 等 SQL 命令超时（秒）</summary>
    public int CommandTimeout { get; set; } = 600;

    /// <summary>BulkCopy / COPY 传输超时（秒）</summary>
    public int BulkCopyTimeout { get; set; } = 600;

    /// <summary>是否启用流式写入（主要影响 SqlBulkCopy）</summary>
    public bool BulkCopyEnableStreaming { get; set; }

    /// <summary>每写入多少行触发通知（SqlBulkCopy.NotifyAfter）</summary>
    public int? BulkCopyNotifyAfter { get; set; }

    /// <summary>每批写入行数；过大占内存，过小增加往返</summary>
    public int? BulkCopyBatchSize { get; set; } = 6000;
}

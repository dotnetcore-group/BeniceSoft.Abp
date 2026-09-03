namespace BeniceSoft.Abp.Sample.Application.Contracts;

public class BulkDemoItemDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string BatchTag { get; set; } = string.Empty;

    public int Version { get; set; }
}

public class BulkDemoResultDto
{
    public string Operation { get; set; } = string.Empty;

    public string BatchTag { get; set; } = string.Empty;

    public int Affected { get; set; }

    public int TotalInBatch { get; set; }

    public List<BulkDemoItemDto> Items { get; set; } = [];

    public long[]? Sequences { get; set; }
}

public interface IBulkSampleAppService
{
    /// <summary>BulkInsert 一批数据。</summary>
    Task<BulkDemoResultDto> BulkInsertAsync(int count = 20);

    /// <summary>BulkUpdate 指定批次数量。</summary>
    Task<BulkDemoResultDto> BulkUpdateAsync(string batchTag, int quantity = 100);

    /// <summary>BulkMerge（存在则更新，不存在则插入）。</summary>
    Task<BulkDemoResultDto> BulkMergeAsync(string? batchTag = null, int count = 10);

    /// <summary>BulkDelete 指定批次。</summary>
    Task<BulkDemoResultDto> BulkDeleteAsync(string batchTag);

    /// <summary>多步 BulkOperation（同事务 Insert + Update）。</summary>
    Task<BulkDemoResultDto> BulkOperationAsync(int count = 10);

    /// <summary>数据库 Sequence 取号。</summary>
    Task<BulkDemoResultDto> GetSequencesAsync(int count = 5);

    /// <summary>FOR UPDATE 锁定查询演示。</summary>
    Task<BulkDemoResultDto> ForUpdateQueryAsync(string batchTag);

    /// <summary>ForceSaveChange 并发重试演示。</summary>
    Task<BulkDemoResultDto> ForceSaveAsync(string batchTag);
}

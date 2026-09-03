namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 分片条件结果
/// </summary>
internal sealed class ShardingResult(bool isShardingKey, string? propertyName)
{

    /// <summary>
    /// 是否是分片字段
    /// </summary>
    public bool IsShardingKey { get; } = isShardingKey;

    /// <summary>
    /// 分片字段名称
    /// </summary>
    public string? PropertyName { get; } = propertyName;
}

public class AverageResult<T>(T sum, long count)
{
    public T Sum { get; } = sum;

    public long Count { get; } = count;
}

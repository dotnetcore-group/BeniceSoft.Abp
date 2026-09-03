namespace BeniceSoft.Abp.Ddd.Application.Contracts;

/// <summary>
/// 审计DTO基类
/// </summary>
[Serializable]
public abstract class BeniceSoftAuditedDto<TKey>
{
    public TKey Id { get; set; } = default!;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreationTime { get; set; }

    /// <summary>
    /// 创建人Id
    /// </summary>
    public long CreatorId { get; set; }

    /// <summary>
    /// 创建人姓名
    /// </summary>
    public string CreatorName { get; set; } = string.Empty;

    /// <summary>
    /// 最新修改时间
    /// </summary>
    public DateTimeOffset? LastModificationTime { get; set; }

    /// <summary>
    /// 最新修改人Id
    /// </summary>
    public long? LastModifierId { get; set; }

    /// <summary>
    /// 最新修改人姓名
    /// </summary>
    public string? LastModifierName { get; set; }
}

/// <summary>
/// 审计DTO基类
/// </summary>
[Serializable]
public abstract class BeniceSoftAuditedDto : BeniceSoftAuditedDto<long>
{
}


namespace BeniceSoft.Abp.Ddd.Domain.Entity;

public interface IBeniceSoftAudited
{
    /// <summary>
    /// 创建时间
    /// </summary>
    DateTimeOffset CreationTime { get; }

    /// <summary>
    /// 创建人Id
    /// </summary>
    long CreatorId { get; }

    /// <summary>
    /// 创建人姓名
    /// </summary>
    string CreatorName { get; }

    /// <summary>
    /// 最新修改时间
    /// </summary>
    DateTimeOffset? LastModificationTime { get; }

    /// <summary>
    /// 最新修改人Id
    /// </summary>
    long? LastModifierId { get; }

    /// <summary>
    /// 最新修改人姓名
    /// </summary>
    string? LastModifierName { get; }
}


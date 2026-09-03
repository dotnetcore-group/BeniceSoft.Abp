using Volo.Abp;

namespace BeniceSoft.Abp.Ddd.Domain.Entity;

public interface IBeniceSoftFullAudited : IBeniceSoftAudited, ISoftDelete
{
    /// <summary>
    /// 删除时间
    /// </summary>
    DateTimeOffset? DeletionTime { get; }

    /// <summary>
    /// 删除人Id
    /// </summary>
    long? DeleterId { get; }

    /// <summary>
    /// 删除人姓名
    /// </summary>
    string? DeleterName { get; }
}


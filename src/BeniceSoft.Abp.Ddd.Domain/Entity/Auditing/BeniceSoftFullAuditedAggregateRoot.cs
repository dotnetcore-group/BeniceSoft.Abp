using Volo.Abp.Domain.Entities;

namespace BeniceSoft.Abp.Ddd.Domain.Entity;

[Serializable]
public abstract class BeniceSoftFullAuditedAggregateRoot : AggregateRoot, IBeniceSoftFullAudited
{
    public virtual DateTimeOffset CreationTime { get; protected set; }
    public virtual long CreatorId { get; protected set; }
    public virtual string CreatorName { get; protected set; } = string.Empty;
    public virtual DateTimeOffset? LastModificationTime { get; protected set; }
    public virtual long? LastModifierId { get; protected set; }
    public virtual string? LastModifierName { get; protected set; }
    public virtual bool IsDeleted { get; protected set; }
    public virtual DateTimeOffset? DeletionTime { get; protected set; }
    public virtual long? DeleterId { get; protected set; }
    public virtual string? DeleterName { get; protected set; }
}

[Serializable]
public abstract class BeniceSoftFullAuditedAggregateRoot<TKey> : AggregateRoot<TKey>, IBeniceSoftFullAudited
{
    public virtual DateTimeOffset CreationTime { get; protected set; }
    public virtual long CreatorId { get; protected set; }
    public virtual string CreatorName { get; protected set; } = string.Empty;
    public virtual DateTimeOffset? LastModificationTime { get; protected set; }
    public virtual long? LastModifierId { get; protected set; }
    public virtual string? LastModifierName { get; protected set; }
    public virtual bool IsDeleted { get; protected set; }
    public virtual DateTimeOffset? DeletionTime { get; protected set; }
    public virtual long? DeleterId { get; protected set; }
    public virtual string? DeleterName { get; protected set; }
}

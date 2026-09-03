namespace BeniceSoft.Abp.Ddd.Domain.Entity;

[Serializable]
public abstract class BeniceSoftAuditedEntity : Volo.Abp.Domain.Entities.Entity, IBeniceSoftAudited
{
    public virtual DateTimeOffset CreationTime { get; protected set; }
    public virtual long CreatorId { get; protected set; }
    public virtual string CreatorName { get; protected set; } = string.Empty;
    public virtual DateTimeOffset? LastModificationTime { get; protected set; }
    public virtual long? LastModifierId { get; protected set; }
    public virtual string? LastModifierName { get; protected set; }
}

[Serializable]
public abstract class BeniceSoftAuditedEntity<TKey> : Volo.Abp.Domain.Entities.Entity<TKey>, IBeniceSoftAudited
{
    public virtual DateTimeOffset CreationTime { get; protected set; }
    public virtual long CreatorId { get; protected set; }
    public virtual string CreatorName { get; protected set; } = string.Empty;
    public virtual DateTimeOffset? LastModificationTime { get; protected set; }
    public virtual long? LastModifierId { get; protected set; }
    public virtual string? LastModifierName { get; protected set; }
}
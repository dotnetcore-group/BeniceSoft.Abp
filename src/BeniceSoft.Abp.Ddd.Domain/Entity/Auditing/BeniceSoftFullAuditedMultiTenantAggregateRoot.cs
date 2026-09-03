using Volo.Abp.MultiTenancy;

namespace BeniceSoft.Abp.Ddd.Domain.Entity;

[Serializable]
public abstract class BeniceSoftFullAuditedMultiTenantAggregateRoot : BeniceSoftFullAuditedAggregateRoot, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }
}

[Serializable]
public abstract class BeniceSoftFullAuditedMultiTenantAggregateRoot<TKey> : BeniceSoftFullAuditedAggregateRoot<TKey>, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }
}
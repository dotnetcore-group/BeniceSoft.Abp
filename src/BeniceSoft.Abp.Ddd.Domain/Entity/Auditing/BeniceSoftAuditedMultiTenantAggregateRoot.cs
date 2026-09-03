using Volo.Abp.MultiTenancy;

namespace BeniceSoft.Abp.Ddd.Domain.Entity;

[Serializable]
public abstract class BeniceSoftAuditedMultiTenantAggregateRoot : BeniceSoftAuditedAggregateRoot, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }
}

[Serializable]
public abstract class BeniceSoftAuditedMultiTenantAggregateRoot<TKey> : BeniceSoftAuditedAggregateRoot<TKey>, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }
}
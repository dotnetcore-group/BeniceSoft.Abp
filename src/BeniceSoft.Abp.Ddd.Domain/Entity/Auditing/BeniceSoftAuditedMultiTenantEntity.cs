using Volo.Abp.MultiTenancy;

namespace BeniceSoft.Abp.Ddd.Domain.Entity;

[Serializable]
public abstract class BeniceSoftAuditedMultiTenantEntity : BeniceSoftAuditedEntity, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }
}

[Serializable]
public abstract class BeniceSoftAuditedMultiTenantEntity<TKey> : BeniceSoftAuditedEntity<TKey>, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }
}
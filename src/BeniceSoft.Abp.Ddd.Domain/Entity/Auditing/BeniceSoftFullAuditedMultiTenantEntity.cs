using Volo.Abp.MultiTenancy;

namespace BeniceSoft.Abp.Ddd.Domain.Entity;

[Serializable]
public abstract class BeniceSoftFullAuditedMultiTenantEntity : BeniceSoftFullAuditedEntity, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }
}

[Serializable]
public abstract class BeniceSoftFullAuditedMultiTenantEntity<TKey> : BeniceSoftFullAuditedEntity<TKey>, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }
}

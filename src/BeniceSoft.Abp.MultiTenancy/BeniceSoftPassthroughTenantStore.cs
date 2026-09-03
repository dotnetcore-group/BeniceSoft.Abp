using BeniceSoft.Core;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace BeniceSoft.Abp.MultiTenancy;

/// <summary>
/// 运行时解析出已认证 claim 的 TenantId 转成有效的 <see cref="TenantConfiguration"/>
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(ITenantStore))]
public class BeniceSoftPassthroughTenantStore : ITenantStore, ITransientDependency
{
    public Task<TenantConfiguration?> FindAsync(string normalizedName)
    {
        return Task.FromResult(Find(normalizedName));
    }

    public Task<TenantConfiguration?> FindAsync(Guid id)
    {
        return Task.FromResult(Find(id));
    }

    public TenantConfiguration? Find(string normalizedName)
    {
        var id = normalizedName.ToGuid();
        return id == Guid.Empty ? null : Create(id);
    }

    public TenantConfiguration? Find(Guid id)
    {
        return Create(id);
    }

    public Task<IReadOnlyList<TenantConfiguration>> GetListAsync(bool includeDetails = false)
    {
        return Task.FromResult<IReadOnlyList<TenantConfiguration>>([]);
    }

    private static TenantConfiguration Create(Guid id)
    {
        var name = id.ToString("D");
        return new TenantConfiguration(id, name, name.ToUpperInvariant())
        {
            IsActive = true
        };
    }
}

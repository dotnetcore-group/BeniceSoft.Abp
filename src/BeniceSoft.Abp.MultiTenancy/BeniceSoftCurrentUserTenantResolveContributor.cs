using BeniceSoft.Abp.Core.Users;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace BeniceSoft.Abp.MultiTenancy;

/// <summary>
/// 从 <see cref="IBeniceSoftCurrentUser"/> 解析租户
/// </summary>
public class BeniceSoftCurrentUserTenantResolveContributor : TenantResolveContributorBase
{
    public const string ContributorName = "BeniceSoftCurrentUser";

    public override string Name => ContributorName;

    public override Task ResolveAsync(ITenantResolveContext context)
    {
        var currentUser = context.ServiceProvider.GetService<IBeniceSoftCurrentUser>();
        if (currentUser is not { IsAuthenticated: true })
        {
            return Task.CompletedTask;
        }

        if (!currentUser.TenantId.HasValue)
        {
            return Task.CompletedTask;
        }

        context.Handled = true;
        context.TenantIdOrName = currentUser.TenantId.Value.ToString("D");
        return Task.CompletedTask;
    }
}

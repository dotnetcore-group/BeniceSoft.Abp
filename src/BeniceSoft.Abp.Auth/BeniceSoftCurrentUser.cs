using BeniceSoft.Abp.Auth.Core;
using BeniceSoft.Abp.Core.Users;
using OpenIddict.Abstractions;
using System.Security.Claims;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Security.Claims;

namespace BeniceSoft.Abp.Auth;

[ExposeServices(typeof(IBeniceSoftCurrentUser))]
public class BeniceSoftCurrentUser : IBeniceSoftCurrentUser, ITransientDependency
{
    private static readonly Claim[] EmptyClaimsArray = [];

    public virtual bool IsAuthenticated => Id.HasValue;

    public virtual long? Id => FindClaim(OpenIddictConstants.Claims.Subject).GetLongValue();

    public virtual Guid? TenantId => FindClaim(AbpClaimTypes.TenantId).GetGuidValue();

    public virtual string ClientId => FindClaimValue(AbpClaimTypes.ClientId) ?? "";

    public virtual string UserName => FindClaimValue(OpenIddictConstants.Claims.Username) ?? "";

    public virtual string NickName => FindClaimValue(OpenIddictConstants.Claims.Nickname) ?? "";

    public virtual string Name => FindClaimValue(OpenIddictConstants.Claims.Name) ?? "";

    public virtual string SurName => FindClaimValue(OpenIddictConstants.Claims.FamilyName) ?? "";

    public virtual string DepartmentName => FindClaimValue(BeniceSoftAuthConstants.ClaimTypes.DepartmentName) ?? "";

    public virtual string PhoneNumber => FindClaimValue(OpenIddictConstants.Claims.PhoneNumber) ?? "";

    public virtual bool PhoneNumberVerified => string.Equals(FindClaimValue(OpenIddictConstants.Claims.PhoneNumberVerified), "true", StringComparison.InvariantCultureIgnoreCase);

    public virtual string Email => FindClaimValue(OpenIddictConstants.Claims.Email) ?? "";

    public virtual bool EmailVerified => string.Equals(FindClaimValue(OpenIddictConstants.Claims.EmailVerified), "true", StringComparison.InvariantCultureIgnoreCase);

    public virtual string[] Roles => FindClaims(OpenIddictConstants.Claims.Role).Select(c => c.Value).Distinct().ToArray();

    public virtual long[] RoleIds => FindClaims(BeniceSoftAuthConstants.ClaimTypes.RoleId)
        .Select(c => c.GetLongValue())
        .Where(x => x.HasValue)
        .Select(x => x!.Value)
        .ToArray();

    private readonly ICurrentPrincipalAccessor _principalAccessor;

    public BeniceSoftCurrentUser(ICurrentPrincipalAccessor principalAccessor)
    {
        _principalAccessor = principalAccessor;
    }

    public virtual Claim? FindClaim(string claimType)
    {
        return _principalAccessor.Principal?.Claims.FirstOrDefault(c => c.Type == claimType);
    }

    public virtual Claim[] FindClaims(string claimType)
    {
        return _principalAccessor.Principal?.Claims.Where(c => c.Type == claimType).ToArray() ?? EmptyClaimsArray;
    }

    public virtual Claim[] GetAllClaims()
    {
        return _principalAccessor.Principal?.Claims.ToArray() ?? EmptyClaimsArray;
    }

    public virtual bool IsInRole(string roleName)
    {
        return Roles.Any(r => r == roleName);
    }

    private string? FindClaimValue(string claimType)
    {
        return FindClaim(claimType)?.Value;
    }
}
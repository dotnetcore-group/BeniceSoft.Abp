using System.Security.Claims;
using BeniceSoft.Core;

namespace BeniceSoft.Abp.Auth.Core;

public static class ClaimExtensions
{
    public static long? GetLongValue(this Claim? claim)
    {
        return claim?.Value.ToInt64();
    }

    public static Guid? GetGuidValue(this Claim? claim)
    {
        if (claim?.Value is null)
        {
            return null;
        }

        return Guid.TryParse(claim.Value, out var result) ? result : null;
    }
}
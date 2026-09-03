using BeniceSoft.Abp.Core;
using Microsoft.AspNetCore.Authentication;

namespace BeniceSoft.Abp.Auth.Authentication;

public class BeniceSoftAuthenticationBuilder
{
    public BeniceSoftAuthenticationBuilder(AuthenticationBuilder authenticationBuilder, BeniceSoftAuthOptions authOptions)
    {
        AuthenticationBuilder = authenticationBuilder;
        AuthOptions = authOptions;
    }

    public virtual AuthenticationBuilder AuthenticationBuilder { get; }

    public virtual BeniceSoftAuthOptions AuthOptions { get; }
}

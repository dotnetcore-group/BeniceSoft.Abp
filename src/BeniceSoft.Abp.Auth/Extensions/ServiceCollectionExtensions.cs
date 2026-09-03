using BeniceSoft.Abp.Auth.Authentication;
using BeniceSoft.Abp.Auth.Authorization;
using BeniceSoft.Abp.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace BeniceSoft.Abp.Auth.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 认证
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static BeniceSoftAuthenticationBuilder AddBeniceSoftAuthentication(this IServiceCollection services)
    {
        var authOptions = new BeniceSoftAuthOptions();
        services.GetConfiguration().GetSection("Auth").Bind(authOptions);
        authOptions = services.ExecutePreConfiguredActions(authOptions);
        services.AddSingleton(_ => authOptions);

        // default scheme is Bearer
        var builder = services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
                {
                    options.Audience = authOptions.Audience;
                    options.Authority = authOptions.Authority;
                    options.IncludeErrorDetails = true;
                    options.RequireHttpsMetadata = false;
                    options.MapInboundClaims = false;// 禁用 claim 类型映射，保留原始的 claim 类型名称（如 "sub" 而不是映射后的长名称）
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = false,
                        // ValidateIssuer = false,
                        RequireExpirationTime = true,
                        RequireAudience = false,
                        // 设置 NameClaimType 和 RoleClaimType，确保 claims 使用正确的类型名称
                        NameClaimType = OpenIddictConstants.Claims.Name,
                        RoleClaimType = OpenIddictConstants.Claims.Role
                    };
                    options.Events = new JwtBearerEvents()
                    {
                        OnAuthenticationFailed = async authenticationFailedContext =>
                        {
                            await authenticationFailedContext.HttpContext.RequestServices
                                .GetRequiredService<OnAuthenticationFailedHandler>()
                                .HandleAsync(authenticationFailedContext);
                        },
                        OnTokenValidated = async tokenValidatedContext =>
                        {
                            await tokenValidatedContext.HttpContext.RequestServices
                                .GetRequiredService<OnTokenValidatedHandler>()
                                .HandleAsync(tokenValidatedContext);
                        }
                    };
                }
            );

        return new BeniceSoftAuthenticationBuilder(builder, authOptions);
    }

    /// <summary>
    /// 授权
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddBeniceSoftAuthorization(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IAuthorizationHandler, BeniceSoftAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            if (!options.DefaultPolicy.Requirements.OfType<BeniceSoftAuthorizationRequirement>().Any())
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .AddRequirements(new BeniceSoftAuthorizationRequirement())
                    .Build();
            }
        });
        return services;
    }
}
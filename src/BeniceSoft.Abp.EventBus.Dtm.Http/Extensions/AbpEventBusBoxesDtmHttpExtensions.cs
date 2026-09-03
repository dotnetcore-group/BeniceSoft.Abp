using Dtmcli;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public static class AbpEventBusBoxesDtmHttpExtensions
{
    public static IServiceCollection AddAbpDtmHttp(this IServiceCollection services,
        Action<DtmHttpOptions> setupAction,
        Action<DtmGlobalTransactionDefaults>? setupGlobalTransactionAction = null)
    {
        services.AddDtmcli((_) => { });
        services.Configure(setupAction);

        if (setupGlobalTransactionAction is not null)
        {
            services.Configure(setupGlobalTransactionAction);
        }

        services.AddHttpClient("dtmClient", (serviceProvider, options) =>
        {
            var dtmHttpOptions = serviceProvider.GetRequiredService<IOptions<DtmHttpOptions>>().Value;

            if (Uri.TryCreate(dtmHttpOptions.DtmUrl, UriKind.Absolute, out var uri))
            {
                options.BaseAddress = uri;
            }

            options.Timeout = TimeSpan.FromMilliseconds(dtmHttpOptions.Timeout > 0 ? dtmHttpOptions.Timeout : 30 * 1000);
        });

        return services;
    }
}


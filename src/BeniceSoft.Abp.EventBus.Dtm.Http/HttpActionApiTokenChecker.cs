using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IActionApiTokenChecker))]
public class HttpActionApiTokenChecker : IActionApiTokenChecker, ITransientDependency
{
    private readonly IOptions<DtmHttpOptions> _dtmHttpOptions;

    public HttpActionApiTokenChecker(IOptions<DtmHttpOptions> dtmHttpOptions)
    {
        _dtmHttpOptions = dtmHttpOptions;
    }

    public Task<bool> IsCorrectAsync(string token)
    {
        return Task.FromResult(token == _dtmHttpOptions.Value.ActionApiToken);
    }
}

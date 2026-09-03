using BeniceSoft.Abp.Core.Users;
using BeniceSoft.Abp.Ddd.Domain;
using Volo.Abp.Application.Services;

namespace BeniceSoft.Abp.Ddd.Application;

public abstract class BeniceSoftApplicationService : ApplicationService
{
    protected IQueryableWrapperFactory QueryableWrapperFactory =>
        LazyServiceProvider.LazyGetRequiredService<IQueryableWrapperFactory>();

    protected new IBeniceSoftCurrentUser CurrentUser =>
        LazyServiceProvider.LazyGetRequiredService<IBeniceSoftCurrentUser>();
}
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public static class DtmTransactionCallbackServiceCollectionExtensions
{
    public static IServiceCollection AddDtmCallbackHandlersFromAssemblies(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies.Where(x => x is not null && !x.IsDynamic).Distinct())
        {
            IEnumerable<Type> types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null)!;
            }

            foreach (var handlerType in types.Where(t => t is { IsClass: true, IsAbstract: false }))
            {
                RegisterHandlerInterfaces(services, handlerType);
            }
        }

        return services;
    }

    private static void RegisterHandlerInterfaces(IServiceCollection services, Type handlerType)
    {
        foreach (var @interface in handlerType.GetInterfaces().Where(i => i.IsGenericType))
        {
            var genericTypeDef = @interface.GetGenericTypeDefinition();
            if (genericTypeDef != typeof(ITccBranchCallbackHandler<>) && genericTypeDef != typeof(ISagaBranchCallbackHandler<>))
            {
                continue;
            }

            var requestType = @interface.GetGenericArguments()[0];
            var (_, handlerName) = DtmBranchMetadataResolver.Resolve(requestType);

            if (genericTypeDef == typeof(ITccBranchCallbackHandler<>))
            {
                RegisterTccHandler(services, handlerType, requestType, handlerName);
                continue;
            }

            RegisterSagaHandler(services, handlerType, requestType, handlerName);
        }
    }

    private static void RegisterTccHandler(IServiceCollection services, Type handlerType, Type requestType, string handlerName)
    {
        services.TryAddTransient(handlerType);
        services.Configure<DtmTransactionCallbackOptions>(options =>
        {
            options.AddTccHandler(handlerType, requestType, handlerName);
        });
    }

    private static void RegisterSagaHandler(IServiceCollection services, Type handlerType, Type requestType, string handlerName)
    {
        services.TryAddTransient(handlerType);
        services.Configure<DtmTransactionCallbackOptions>(options =>
        {
            options.AddSagaHandler(handlerType, requestType, handlerName);
        });
    }
}

using BeniceSoft.Abp.Auth.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Reflection;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore;

public static class RowPermissionDbContextOptionsExtensions
{
    /// <summary>
    /// 注册带权限的仓储实现
    /// </summary>
    public static IServiceCollection AddRowPermissionRepositories<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext, IEfCoreDbContext
    {
        var dbContextType = typeof(TDbContext);
        var entityTypes = GetEntityTypes(dbContextType);

        foreach (var entityType in entityTypes)
        {
            RegisterRowPermissionRepository(services, dbContextType, entityType);
        }

        return services;
    }

    private static IEnumerable<Type> GetEntityTypes(Type dbContextType)
    {
        return from property in dbContextType.GetTypeInfo().GetProperties(BindingFlags.Public | BindingFlags.Instance)
               where ReflectionHelper.IsAssignableToGenericType(property.PropertyType, typeof(DbSet<>)) &&
                     typeof(IEntity).IsAssignableFrom(property.PropertyType.GenericTypeArguments[0])
               select property.PropertyType.GenericTypeArguments[0];
    }

    private static void RegisterRowPermissionRepository(IServiceCollection services, Type dbContextType, Type entityType)
    {
        var primaryKeyType = EntityHelper.FindPrimaryKeyType(entityType);
        if (primaryKeyType == null)
        {
            return;
        }

        var repositoryImplementationType = typeof(RowPermissionEfCoreRepository<,,>)
            .MakeGenericType(dbContextType, entityType, primaryKeyType);

        var authRepositoryInterfaceWithKey = typeof(IRowPermissionRepository<,>)
            .MakeGenericType(entityType, primaryKeyType);
        services.TryAddTransient(authRepositoryInterfaceWithKey, repositoryImplementationType);

        if (primaryKeyType == typeof(long))
        {
            var authRepositoryInterface = typeof(IRowPermissionRepository<>).MakeGenericType(entityType);
            var repositoryImplementationTypeForLong = typeof(RowPermissionEfCoreRepository<,>)
                .MakeGenericType(dbContextType, entityType);
            services.TryAddTransient(authRepositoryInterface, repositoryImplementationTypeForLong);
        }
    }
}


using BeniceSoft.Abp.Auth.Core;
using BeniceSoft.Abp.Auth.Repository;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore;

/// <summary>
/// 带数据权限的 EF Core 仓储基类
/// </summary>
/// <typeparam name="TDbContext">DbContext 类型</typeparam>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public class RowPermissionEfCoreRepository<TDbContext, TEntity, TKey> : EfCoreRepository<TDbContext, TEntity, TKey>, IRowPermissionRepository<TEntity, TKey>
    where TDbContext : IEfCoreDbContext
    where TEntity : class, IEntity<TKey>
{
    protected ICurrentUserPermissionAccessor CurrentUserPermissionAccessor
        => LazyServiceProvider.LazyGetRequiredService<ICurrentUserPermissionAccessor>();

    public RowPermissionEfCoreRepository(IDbContextProvider<TDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public override async Task<IQueryable<TEntity>> GetQueryableAsync()
    {
        var queryable = await base.GetQueryableAsync();
        return await ApplyFilterAsync(queryable);
    }

    /// <summary>
    /// 获取不带数据权限过滤的 Queryable
    /// </summary>
    public Task<IQueryable<TEntity>> GetQueryableWithoutRowFilterAsync()
    {
        return base.GetQueryableAsync();
    }

    /// <summary>
    /// 应用行权限过滤
    /// </summary>
    protected virtual async Task<IQueryable<TEntity>> ApplyFilterAsync(IQueryable<TEntity> queryable)
    {
        var userPermission = CurrentUserPermissionAccessor.UserPermission;
        if (userPermission?.RowPermissions == null || !userPermission.RowPermissions.Any())
        {
            return queryable;
        }

        var dbContext = await GetDbContextAsync() as DbContext;
        var tableName = dbContext?.Model.FindEntityType(typeof(TEntity))?.GetTableName();
        if (string.IsNullOrEmpty(tableName))
        {
            return queryable;
        }

        var rowPermissions = userPermission.RowPermissions
            .Where(c => c.TableName == tableName)
            .ToList();
        if (rowPermissions == null || !rowPermissions.Any())
        {
            return queryable;
        }

        var filterExp = RepositoryExtensions.BuildRowPermissionPredicate<TEntity>(rowPermissions, userPermission.UserId);
        return queryable.Where(filterExp);
    }
}

/// <summary>
/// 带数据权限的 EF Core 仓储基类（主键默认long类型）
/// </summary>
public class RowPermissionEfCoreRepository<TDbContext, TEntity> : RowPermissionEfCoreRepository<TDbContext, TEntity, long>, IRowPermissionRepository<TEntity>
    where TDbContext : IEfCoreDbContext
    where TEntity : class, IEntity<long>
{
    public RowPermissionEfCoreRepository(IDbContextProvider<TDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}


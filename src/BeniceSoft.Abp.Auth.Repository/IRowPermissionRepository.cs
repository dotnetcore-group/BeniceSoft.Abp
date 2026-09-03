using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace BeniceSoft.Abp.Auth.Repository;

/// <summary>
/// 带数据权限的仓储接口
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public interface IRowPermissionRepository<TEntity, TKey> : IRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
{
    /// <summary>
    /// 获取不带数据权限过滤的 Queryable
    /// </summary>
    Task<IQueryable<TEntity>> GetQueryableWithoutRowFilterAsync();
}

/// <summary>
/// 带数据权限的仓储接口（主键默认long类型）
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public interface IRowPermissionRepository<TEntity> : IRowPermissionRepository<TEntity, long>
    where TEntity : class, IEntity<long>
{
}


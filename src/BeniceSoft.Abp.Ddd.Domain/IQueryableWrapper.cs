using System.Linq.Expressions;
using BeniceSoft.Core;
using BeniceSoft.Extensions.DynamicQuery;

namespace BeniceSoft.Abp.Ddd.Domain;

/// <summary>
/// 查询包装器
/// </summary>
/// <typeparam name="TEntity"></typeparam>
public interface IQueryableWrapper<TEntity> where TEntity : class
{
    /// <summary>
    /// 不追踪查询
    /// </summary>
    /// <returns></returns>
    IQueryableWrapper<TEntity> AsNoTracking();

    /// <summary>
    /// 加载导航属性
    /// </summary>
    /// <param name="propertySelectors"></param>
    /// <returns></returns>
    IQueryableWrapper<TEntity> Include(params Expression<Func<TEntity, object>>[] propertySelectors);

    /// <summary>
    /// 正序排序
    /// </summary>
    /// <param name="keySelector"></param>
    /// <returns></returns>
    IQueryableWrapper<TEntity> OrderBy<TKey>(Expression<Func<TEntity, TKey>> keySelector);

    /// <summary>
    /// 倒序排序
    /// </summary>
    /// <param name="keySelector"></param>
    /// <typeparam name="TKey"></typeparam>
    /// <returns></returns>
    IQueryableWrapper<TEntity> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector);

    /// <summary>
    /// 通过关键字查询
    /// </summary>
    /// <param name="searchKey"></param>
    /// <param name="propertySelectors"></param>
    /// <returns></returns>
    IQueryableWrapper<TEntity> SearchByKey(string? searchKey, params Expression<Func<TEntity, object?>>[] propertySelectors);

    /// <summary>
    /// 条件查询
    /// </summary>
    /// <param name="condition"></param>
    /// <param name="predicate"></param>
    /// <returns></returns>
    IQueryableWrapper<TEntity> WhereIf(bool condition, Expression<Func<TEntity, bool>> predicate);

    /// <summary>
    /// 动态查询
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    IQueryableWrapper<TEntity> DynamicQueryBy(IDynamicQueryRequest request);

    /// <summary>
    /// 分页（使用 skipCount 和 maxResultCount）
    /// </summary>
    /// <param name="skipCount"></param>
    /// <param name="maxResultCount"></param>
    /// <returns></returns>
    IQueryableWrapper<TEntity> PageBy(int skipCount, int maxResultCount);

    /// <summary>
    /// 分页（使用页码和每页大小）
    /// </summary>
    /// <param name="pageNumber">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns></returns>
    IQueryableWrapper<TEntity> PageByNumber(int pageNumber, int pageSize);

    /// <summary>
    /// 转换成 Queryable
    /// </summary>
    /// <returns></returns>
    IQueryable<TEntity> AsQueryable();

    /// <summary>
    /// 执行查询
    /// </summary>
    /// <returns></returns>
    Task<List<TEntity>> ToListAsync();

    /// <summary>
    /// 总数
    /// </summary>
    /// <returns></returns>
    Task<int> CountAsync();

    /// <summary>
    /// 获取第一个
    /// </summary>
    /// <returns></returns>
    Task<TEntity?> FirstOrDefaultAsync();

    /// <summary>
    /// 根据条件获取第一个
    /// </summary>
    /// <param name="predicate"></param>
    /// <returns></returns>
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate);

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <param name="pageNumber"></param>
    /// <param name="pageSize"></param>
    /// <returns></returns>
    Task<PagedList<TEntity>> ToPagedListAsync(int pageNumber, int pageSize);
}
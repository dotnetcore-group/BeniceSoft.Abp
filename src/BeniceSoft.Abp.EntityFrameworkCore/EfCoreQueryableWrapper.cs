using BeniceSoft.Abp.Ddd.Domain;
using BeniceSoft.Abp.Extensions.DynamicQuery.EfCore.Extensions;
using BeniceSoft.Core;
using BeniceSoft.Extensions.DynamicQuery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Volo.Abp;

namespace BeniceSoft.Abp.EntityFrameworkCore;

public class EfCoreQueryableWrapper<TEntity> : IQueryableWrapper<TEntity> where TEntity : class
{
    private IQueryable<TEntity> _queryable;

    public EfCoreQueryableWrapper(IQueryable<TEntity> queryable)
    {
        _queryable = queryable;
    }

    public IQueryableWrapper<TEntity> AsNoTracking()
    {
        _queryable = _queryable.AsNoTracking();
        return this;
    }

    public IQueryableWrapper<TEntity> Include(params Expression<Func<TEntity, object>>[] propertySelectors)
    {
        if (!propertySelectors.IsNullOrEmpty())
        {
            foreach (var propertySelector in propertySelectors)
            {
                _queryable = _queryable.Include(propertySelector);
            }
        }

        return this;
    }

    public IQueryableWrapper<TEntity> OrderBy<TKey>(Expression<Func<TEntity, TKey>> keySelector)
    {
        _queryable = _queryable.OrderBy(keySelector);
        return this;
    }

    public IQueryableWrapper<TEntity> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> keySelector)
    {
        _queryable = _queryable.OrderByDescending(keySelector);
        return this;
    }

    public IQueryableWrapper<TEntity> SearchByKey(string? searchKey, params Expression<Func<TEntity, object?>>[] propertySelectors)
    {
        if (!searchKey.IsNullOrWhiteSpace() && propertySelectors.Any())
        {
            var searchExp = Expression.Constant(searchKey);

            // 最终查询的条件
            var predicateExp = Expression.Lambda<Func<TEntity, bool>>(_falseConstantExp, _entityParameterExp);

            foreach (LambdaExpression propertySelector in propertySelectors)
            {
                var memberInfo = propertySelector.GetMemberAccess();
                if (memberInfo.MemberType != MemberTypes.Property)
                {
                    throw new AbpException($"{propertySelector}不是有效的属性");
                }

                var propertyInfo = (memberInfo as PropertyInfo)!;
                Expression propertyExp = Expression.Property(_entityParameterExp, propertyInfo);
                var nullCheckExp = BuildNullCheckExpression(propertyExp);

                if (propertyInfo.PropertyType != typeof(string))
                {
                    var toStringMethodInfo = GetToStringMethodInfo(propertyInfo.PropertyType);
                    propertyExp = Expression.Call(propertyExp, toStringMethodInfo);
                }

                var callExp = Expression.Call(propertyExp, ContainsMethodInfo, searchExp);
                var predicate = Expression.AndAlso(nullCheckExp, callExp);
                var lambdaExp = Expression.Lambda<Func<TEntity, bool>>(predicate, _entityParameterExp);

                predicateExp = PredicateBuilder.Or(predicateExp, lambdaExp);
            }

            _queryable = _queryable.Where(predicateExp);
        }

        return this;
    }

    public IQueryableWrapper<TEntity> WhereIf(bool condition, Expression<Func<TEntity, bool>> predicate)
    {
        if (condition)
        {
            _queryable = _queryable.Where(predicate);
        }

        return this;
    }

    public IQueryableWrapper<TEntity> DynamicQueryBy(IDynamicQueryRequest request)
    {
        _queryable = _queryable.DynamicQueryBy(request, skipNullableCheck: true);

        return this;
    }

    public IQueryableWrapper<TEntity> PageBy(int skipCount, int maxResultCount)
    {
        if (skipCount < 0)
        {
            throw new ArgumentException("skipCount cannot be less then 0");
        }

        _queryable = _queryable.Skip(skipCount).Take(maxResultCount);

        return this;
    }

    public IQueryableWrapper<TEntity> PageByNumber(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentException("pageNumber must be greater than or equal to 1", nameof(pageNumber));
        }

        if (pageSize < 1)
        {
            throw new ArgumentException("pageSize must be greater than or equal to 1", nameof(pageSize));
        }

        var skipCount = (pageNumber - 1) * pageSize;
        _queryable = _queryable.Skip(skipCount).Take(pageSize);

        return this;
    }

    public IQueryable<TEntity> AsQueryable() => _queryable;

    public Task<List<TEntity>> ToListAsync() => _queryable.ToListAsync();

    public Task<int> CountAsync() => _queryable.CountAsync();

    public Task<TEntity?> FirstOrDefaultAsync() => _queryable.FirstOrDefaultAsync();

    public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate) => _queryable.FirstOrDefaultAsync(predicate);

    public async Task<PagedList<TEntity>> ToPagedListAsync(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentException("pageNumber must be greater than or equal to 1", nameof(pageNumber));
        }

        if (pageSize < 1)
        {
            throw new ArgumentException("pageSize must be greater than or equal to 1", nameof(pageSize));
        }

        var skipCount = (pageNumber - 1) * pageSize;
        var count = await _queryable.CountAsync();
        if (count > 0)
        {
            var items = await _queryable.Skip(skipCount).Take(pageSize).ToArrayAsync();
            return new PagedList<TEntity>(count, items);
        }

        return new PagedList<TEntity>(0, []);
    }

    #region Private Methods

    private static Expression BuildNullCheckExpression(Expression propertyExp)
    {
        var isNullable = !propertyExp.Type.IsValueType ||
                         Nullable.GetUnderlyingType(propertyExp.Type) is not null;

        if (isNullable)
        {
            return Expression.NotEqual(
                propertyExp,
                Expression.Constant(propertyExp.Type.GetDefaultValue(),
                    propertyExp.Type));
        }

        return Expression.Constant(true, typeof(bool));
    }

    // ReSharper disable once StaticMemberInGenericType
    private static readonly MethodInfo ContainsMethodInfo = typeof(string)
        .GetMethod(nameof(string.Contains), [typeof(string)])!;

    // 缓存不同类型的 ToString 方法
    private static readonly ConcurrentDictionary<Type, MethodInfo> ToStringMethodInfoCache = new();

    private static MethodInfo GetToStringMethodInfo(Type type)
    {
        return ToStringMethodInfoCache.GetOrAdd(type, t => t.GetMethod(nameof(object.ToString), Type.EmptyTypes)!);
    }

    private readonly ConstantExpression _falseConstantExp = Expression.Constant(false);
    private readonly ParameterExpression _entityParameterExp = Expression.Parameter(typeof(TEntity), "x");

    #endregion Private Methods
}

using System.Linq.Expressions;
using System.Reflection;
using BeniceSoft.Abp.Auth.Core.Models;
using BeniceSoft.Abp.Ddd.Domain.Entity;
using BeniceSoft.Core;
using Volo.Abp.Domain.Entities;

namespace BeniceSoft.Abp.Auth.Repository;

public static class RepositoryExtensions
{
    /// <summary>
    /// 根据行权限配置生成过滤表达式
    /// </summary>
    public static Expression<Func<TEntity, bool>> BuildRowPermissionPredicate<TEntity>(List<RowPermission> data, long? userId)
        where TEntity : class, IEntity
    {
        // 添加业务单据的 与我有关的权限
        var groupExpList = new List<Expression<Func<TEntity, bool>>>();
        if (typeof(IHaveOwnerId).IsAssignableFrom(typeof(TEntity)))
        {
            if (userId.HasValue)
            {
                var ownerExp = ExprBuilder.Create<TEntity>(nameof(IHaveOwnerId.OwnerId), userId, ExprOperator.Equal);
                groupExpList.Add(ownerExp);
            }
        }

        foreach (var dataPermission in data)
        {
            foreach (var group in dataPermission.ConditionGroups)
            {
                Expression<Func<TEntity, bool>>? currentGroupExp = ExprBuilder.True<TEntity>();
                foreach (var condition in group.Conditions)
                {
                    // 当前字段拥有超管权限，默认查询所有数据
                    if (condition.IsDataSuperAdmin)
                    {
                        continue;
                    }

                    // 不是超管，又没有勾选具体的值，一条数据都查不到
                    if (condition.Values.Count < 1)
                    {
                        currentGroupExp = ObjectExtensions.And(currentGroupExp, ExprBuilder.False<TEntity>());
                    }
                    else
                    {
                        // 组内条件表达式
                        _ = int.TryParse(condition.Operator, out int result);
                        var op = (ExprOperator)result;

                        // 获取实体属性的类型，将字符串值转换为正确的类型
                        var propertyType = GetPropertyType<TEntity>(condition.ColumnName);

                        // In/NotIn 操作符使用 List，其他操作符使用单个值
                        object value;
                        if (op is ExprOperator.In or ExprOperator.NotIn or ExprOperator.Between)
                        {
                            value = ConvertValues(condition.Values, propertyType);
                        }
                        else
                        {
                            value = ConvertSingleValue(condition.Values[0], propertyType);
                        }

                        currentGroupExp = currentGroupExp.And(condition.ColumnName, value, op);
                    }
                }

                if (currentGroupExp != null)
                {
                    groupExpList.Add(currentGroupExp);
                }
            }
        }

        var filterExpression = groupExpList.FirstOrDefault();
        foreach (var item in groupExpList)
        {
            if (item == filterExpression)
            {
                continue;
            }

            filterExpression = filterExpression != null ? ObjectExtensions.Or(filterExpression, item) : null;
        }

        if (filterExpression == null)
        {
            filterExpression = ExprBuilder.False<TEntity>();
        }

        return filterExpression;
    }

    /// <summary>
    /// 获取实体属性的类型
    /// </summary>
    private static Type GetPropertyType<TEntity>(string propertyName)
    {
        var type = typeof(TEntity);
        foreach (var part in propertyName.Split('.'))
        {
            var prop = type.GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null) return typeof(string);
            type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        }
        return type;
    }

    /// <summary>
    /// 将字符串值列表转换为目标类型的列表
    /// </summary>
    private static object ConvertValues(List<string> values, Type targetType)
    {
        if (targetType == typeof(string)) return values;

        var listType = typeof(List<>).MakeGenericType(targetType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
        foreach (var v in values)
        {
            list.Add(ConvertSingleValue(v, targetType));
        }
        return list;
    }

    /// <summary>
    /// 将单个字符串值转换为目标类型
    /// </summary>
    private static object ConvertSingleValue(string value, Type targetType)
    {
        if (targetType == typeof(string)) return value;
        if (targetType.IsEnum) return Enum.Parse(targetType, value);
        return Convert.ChangeType(value, targetType);
    }
}
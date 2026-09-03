using System.Collections.Concurrent;
using BeniceSoft.Abp.Auth.Core;
using BeniceSoft.Abp.Auth.Core.Models;
using BeniceSoft.Core;
using BeniceSoft.Core.Reflector;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application.Dtos;

namespace BeniceSoft.Abp.Auth.Permissions;

/// <summary>
/// 字段权限过滤器
/// </summary>
public class FieldAuthFilterAttribute : ActionFilterAttribute
{
    /// <summary>
    /// 缓存类型的属性反射器
    /// </summary>
    private static readonly ConcurrentDictionary<Type, PropertyReflector[]> PropertyReflectorCache = new();

    /// <summary>
    /// 缓存类型的数据提取器
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Func<object, object?>> DataExtractorCache = new();

    /// <summary>
    /// Action执行后
    /// </summary>
    /// <param name="context"></param>
    public override void OnActionExecuted(ActionExecutedContext context)
    {
        base.OnActionExecuted(context);

        if (context.Result is not ObjectResult data)
        {
            return;
        }

        var myJson = data.Value;
        var subObject = myJson?.GetType();
        if (subObject == null)
        {
            return;
        }

        // 获取当前请求用户的字段权限配置
        var currentUserPermissionAccessor = context.HttpContext.RequestServices
            .GetRequiredService<ICurrentUserPermissionAccessor>();
        var fieldCfg = currentUserPermissionAccessor.UserPermission?.FieldPermissions;
        if (!(fieldCfg?.Any() ?? false))
        {
            return;
        }

        var dic = fieldCfg.ToDictionary(c => c.TableName + "." + c.FieldName);

        // 使用缓存的数据提取器
        var extractor = GetDataExtractor(subObject);
        var tData = extractor(myJson!);
        if (tData != null)
        {
            FilterFields(tData, dic);
        }
    }

    /// <summary>
    /// 获取数据提取器
    /// </summary>
    private static Func<object, object?> GetDataExtractor(Type type)
    {
        return DataExtractorCache.GetOrAdd(type, t =>
        {
            if (t.IsAssignableTo(typeof(BaseResponse)))
            {
                // BaseResponse 类型，提取 Data 属性
                var dataProperty = t.GetProperty("Data");
                if (dataProperty != null)
                {
                    var propReflector = dataProperty.GetReflector();
                    return obj => propReflector.GetValue(obj);
                }
            }
            else if (t.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IListResult<>)))
            {
                // IListResult 类型，提取 Items 属性
                var itemsProperty = t.GetProperty("Items");
                if (itemsProperty != null)
                {
                    var propReflector = itemsProperty.GetReflector();
                    return obj => propReflector.GetValue(obj);
                }
            }

            // 其他类型，直接返回对象本身
            return obj => obj;
        });
    }

    /// <summary>
    /// 获取类型的属性反射器数组
    /// </summary>
    private static PropertyReflector[] GetPropertyReflectors(Type type)
    {
        return PropertyReflectorCache.GetOrAdd(type, t =>
            t.GetProperties()
             .Select(p => p.GetReflector())
             .ToArray());
    }

    private void FilterFields(object? tData, Dictionary<string, FieldPermission> dic)
    {
        if (tData is null)
        {
            return;
        }

        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(tData?.GetType()))
        {
            foreach (var item in (System.Collections.IEnumerable)tData!)
            {
                var itemType = item.GetType();
                if (!itemType.IsClass)
                {
                    continue; //防止传入的对象是List<object>,需要进行递归查找下一个类型是class的item
                }

                var propReflectors = GetPropertyReflectors(itemType);
                MaskedAttrValue(item, propReflectors, dic);
            }
        }
        else
        {
            var propReflectors = GetPropertyReflectors(tData!.GetType());
            MaskedAttrValue(tData, propReflectors, dic);
        }
    }

    /// <summary>
    /// 根据字段权限配置，隐藏字段的真实值
    /// </summary>
    /// <param name="obj">dto对象本身</param>
    /// <param name="propertyReflectors">DTO对象的所有属性反射器集合</param>
    /// <param name="keyValuePairs">字段权限配置项</param>
    private void MaskedAttrValue(object obj, PropertyReflector[] propertyReflectors, Dictionary<string, FieldPermission> keyValuePairs)
    {
        foreach (var propReflector in propertyReflectors)
        {
            var propInfo = propReflector.GetMemberInfo();
            if (!propInfo.PropertyType.IsSimpleType())
            {
                var propertyValue = propReflector.GetValue(obj);
                if (propertyValue is not null)
                {
                    FilterFields(propertyValue, keyValuePairs);
                }

                continue;
            }

            var attr = propReflector.GetCustomAttribute<FieldAuthAttribute>();
            if (attr == null)
            {
                continue;
            }

            var attrVal = attr.Description;
            if (keyValuePairs.TryGetValue(attrVal, out var fieldPermission))
            {
                if (!fieldPermission.IsDisplay)
                {
                    propReflector.SetValue(obj, null!);
                }
            }
        }
    }
}

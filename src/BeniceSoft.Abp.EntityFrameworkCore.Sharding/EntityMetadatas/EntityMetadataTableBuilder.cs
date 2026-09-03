using BeniceSoft.Core;
using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public class EntityMetadataTableBuilder<T>(EntityMetadata metadata)
    where T : class
{
    /// <summary>
    /// 设置分表字段
    /// </summary>
    /// <typeparam name="TProperty"></typeparam>
    /// <param name="property"></param>
    /// <returns></returns>
    public EntityMetadataTableBuilder<T> WithProperty<TProperty>(Expression<Func<T, TProperty>> property)
    {
        var propertyInfo = property.GetProperty()
            ?? throw new ArgumentException("Unable to resolve property from expression.", nameof(property));
        metadata.SetTableProperty(propertyInfo);
        return this;
    }

    /// <summary>
    /// 设置分表字段
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public EntityMetadataTableBuilder<T> WithProperty(string propertyName)
    {
        var propertyInfo = typeof(T).GetShadowingProperty(propertyName)
            ?? throw new ArgumentException($"Unable to resolve property [{propertyName}].", nameof(propertyName));
        metadata.SetTableProperty(propertyInfo);
        return this;
    }

    /// <summary>
    /// 添加额外的分表字段
    /// </summary>
    /// <typeparam name="TProperty"></typeparam>
    /// <param name="property"></param>
    /// <returns></returns>
    public EntityMetadataTableBuilder<T> AddProperty<TProperty>(Expression<Func<T, TProperty>> property)
    {
        var propertyInfo = property.GetProperty()
            ?? throw new ArgumentException("Unable to resolve property from expression.", nameof(property));
        metadata.AddTableProperty(propertyInfo);
        return this;
    }

    /// <summary>
    /// 添加额外的分表字段
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public EntityMetadataTableBuilder<T> AddProperty(string propertyName)
    {
        var propertyInfo = typeof(T).GetShadowingProperty(propertyName)
            ?? throw new ArgumentException($"Unable to resolve property [{propertyName}].", nameof(propertyName));
        metadata.AddTableProperty(propertyInfo);
        return this;
    }

    /// <summary>
    /// 启动时是否建表
    /// </summary>
    public EntityMetadataTableBuilder<T> WithAutoCreate(bool? autoCreate)
    {
        metadata.AutoCreateTable = autoCreate;
        return this;
    }

    /// <summary>
    /// 分表的表和后缀连接器(默认下滑杠)
    /// </summary>
    /// <param name="separator"></param>
    /// <returns></returns>
    public EntityMetadataTableBuilder<T> WithSeparator(string separator)
    {
        metadata.TableSeparator = separator;
        return this;
    }
}

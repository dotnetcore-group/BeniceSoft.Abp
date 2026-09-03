using BeniceSoft.Core;
using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public class EntityMetadataDataSourceBuilder<T>(EntityMetadata metadata)
    where T : class
{
    /// <summary>
    /// 设置分库字段
    /// </summary>
    /// <typeparam name="TProperty"></typeparam>
    /// <param name="property"></param>
    /// <returns></returns>
    public EntityMetadataDataSourceBuilder<T> WithProperty<TProperty>(Expression<Func<T, TProperty>> property)
    {
        var propertyInfo = property.GetProperty()
            ?? throw new ArgumentException("Unable to resolve property from expression.", nameof(property));
        metadata.SetDataSourceProperty(propertyInfo);
        return this;
    }

    /// <summary>
    /// 设置分库字段
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public EntityMetadataDataSourceBuilder<T> WithProperty(string propertyName)
    {
        var propertyInfo = typeof(T).GetShadowingProperty(propertyName)
            ?? throw new ArgumentException($"Unable to resolve property [{propertyName}].", nameof(propertyName));
        metadata.SetDataSourceProperty(propertyInfo);
        return this;
    }

    /// <summary>
    /// 添加额外的分库字段
    /// </summary>
    /// <typeparam name="TProperty"></typeparam>
    /// <param name="property"></param>
    /// <returns></returns>
    public EntityMetadataDataSourceBuilder<T> AddProperty<TProperty>(Expression<Func<T, TProperty>> property)
    {
        var propertyInfo = property.GetProperty()
            ?? throw new ArgumentException("Unable to resolve property from expression.", nameof(property));
        metadata.AddDataSourceProperty(propertyInfo);
        return this;
    }

    /// <summary>
    /// 添加额外的分库字段
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public EntityMetadataDataSourceBuilder<T> AddProperty(string propertyName)
    {
        var propertyInfo = typeof(T).GetShadowingProperty(propertyName)
            ?? throw new ArgumentException($"Unable to resolve property [{propertyName}].", nameof(propertyName));
        metadata.AddDataSourceProperty(propertyInfo);
        return this;
    }

    /// <summary>
    /// 启动时是否建库
    /// </summary>
    public EntityMetadataDataSourceBuilder<T> WithAutoCreate(bool? autoCreate)
    {
        metadata.AutoCreateDataSource = autoCreate;
        return this;
    }
}

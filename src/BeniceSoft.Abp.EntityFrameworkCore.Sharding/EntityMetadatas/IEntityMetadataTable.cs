namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 对象元数据分表配置 用来配置分表对象的一些信息
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IEntityMetadataTable<T>
    where T : class
{
    /// <summary>
    /// 配置分表对象
    /// </summary>
    /// <param name="builder"></param>
    void Configure(EntityMetadataTableBuilder<T> builder);
}

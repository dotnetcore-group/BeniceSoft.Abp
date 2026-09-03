namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 对象元数据分库配置 用来配置分库对象的一些信息
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IEntityMetadataDataSource<T>
    where T : class
{
    /// <summary>
    /// 配置分库对象
    /// </summary>
    /// <param name="builder"></param>
    void Configure(EntityMetadataDataSourceBuilder<T> builder);
}

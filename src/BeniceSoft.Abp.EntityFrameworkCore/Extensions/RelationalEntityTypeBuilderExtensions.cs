using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// 为了避免去修改自动生成的迁移文件类，所以此扩展类所在命名空间直接使用 Microsoft.EntityFrameworkCore
/// </summary>
public static class RelationalEntityTypeBuilderExtensions
{
    /// <summary>
    /// 解决 使用表包含的时候，无法增加注释的问题
    /// A 包含（Owned） B ，但要 B 额外创建表
    /// 但是 B 无法使用 EntityTypeBuilder 扩展里的  HasComment
    /// 得使用 OwnedNavigationBuilder 里的扩展 操作他自己里的 Metadata 的 Annotation 对象才能增加
    /// RelationalAnnotationNames.Comment 在 EF 是注释的 title
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TRelatedEntity"></typeparam>
    /// <param name="builder"></param>
    /// <param name="comment"></param>
    /// <returns></returns>
    public static OwnedNavigationBuilder<TEntity, TRelatedEntity> HasComment<TEntity, TRelatedEntity>(
        this OwnedNavigationBuilder<TEntity, TRelatedEntity> builder,
        string comment)
        where TEntity : class
        where TRelatedEntity : class
    {
        builder.HasAnnotation(RelationalAnnotationNames.Comment, comment);
        return builder;
    }

    /// <summary>
    /// 解决 Script-Migration 时候的报错问题
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="comment"></param>
    /// <returns></returns>
    public static OwnedNavigationBuilder HasComment(
        this OwnedNavigationBuilder builder,
        string comment)
    {
        builder.HasAnnotation(RelationalAnnotationNames.Comment, comment);
        return builder;
    }
}

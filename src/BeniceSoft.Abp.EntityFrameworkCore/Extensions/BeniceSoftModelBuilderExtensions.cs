using BeniceSoft.Abp.Ddd.Domain.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.EntityFrameworkCore.ValueComparers;
using Volo.Abp.EntityFrameworkCore.ValueConverters;

namespace BeniceSoft.Abp.EntityFrameworkCore;

public static class BeniceSoftModelBuilderExtensions
{
    public static void ConfigureBeniceSoftConventions(this EntityTypeBuilder builder, INameRewriter nameRewriter)
    {
        builder.TryConfigureExtraProperties(nameRewriter);
        builder.TryConfigureConcurrencyStamp(nameRewriter);

        builder.TryConfigureAuditedProperties(nameRewriter);
        builder.TryConfigureSoftDelete(nameRewriter);
        builder.TryConfigurOwnerId(nameRewriter);
    }

    public static void TryConfigureExtraProperties(this EntityTypeBuilder b, INameRewriter nameRewriter)
    {
        if (!b.Metadata.ClrType.IsAssignableTo<IHasExtraProperties>())
        {
            return;
        }

        var converterType = typeof(ExtraPropertiesValueConverter<>).MakeGenericType(b.Metadata.ClrType);
        var extraPropertiesValueConverter = (ValueConverter<ExtraPropertyDictionary, string>)Activator.CreateInstance(converterType)!;

        var columnName = nameRewriter.RewriteName(nameof(IHasExtraProperties.ExtraProperties));
        b.Property<ExtraPropertyDictionary>(nameof(IHasExtraProperties.ExtraProperties))
            .HasColumnName(columnName)
            .HasConversion(extraPropertiesValueConverter)
            .HasComment("扩展属性")
            .Metadata.SetValueComparer(new ExtraPropertyDictionaryValueComparer());

        b.TryConfigureObjectExtensions();
    }

    public static void TryConfigureConcurrencyStamp(this EntityTypeBuilder b, INameRewriter nameRewriter)
    {
        if (!b.Metadata.ClrType.IsAssignableTo<IHasConcurrencyStamp>())
        {
            return;
        }

        var columnName = nameRewriter.RewriteName(nameof(IHasConcurrencyStamp.ConcurrencyStamp));
        b.Property(nameof(IHasConcurrencyStamp.ConcurrencyStamp))
            .IsConcurrencyToken()
            .HasMaxLength(ConcurrencyStampConsts.MaxLength)
            .HasColumnName(columnName);
    }

    public static void TryConfigureAuditedProperties(this EntityTypeBuilder b, INameRewriter nameRewriter)
    {
        if (b.Metadata.ClrType.IsAssignableTo<IBeniceSoftAudited>())
        {
            var creatorIdColumnName = nameRewriter.RewriteName(nameof(IBeniceSoftAudited.CreatorId));
            b.Property(nameof(IBeniceSoftAudited.CreatorId))
                .IsRequired()
                .HasColumnName(creatorIdColumnName)
                .HasComment("创建人Id");

            var creatorNameColumnName = nameRewriter.RewriteName(nameof(IBeniceSoftAudited.CreatorName));
            b.Property(nameof(IBeniceSoftAudited.CreatorName))
                .IsRequired()
                .HasColumnName(creatorNameColumnName)
                .HasComment("创建人姓名");

            var creationTimeColumnName = nameRewriter.RewriteName(nameof(IBeniceSoftAudited.CreationTime));
            b.Property(nameof(IBeniceSoftAudited.CreationTime))
                .IsRequired()
                .HasColumnName(creationTimeColumnName)
                .HasComment("创建时间");

            var modifierIdColumnName = nameRewriter.RewriteName(nameof(IBeniceSoftAudited.LastModifierId));
            b.Property(nameof(IBeniceSoftAudited.LastModifierId))
                .IsRequired(false)
                .HasColumnName(modifierIdColumnName)
                .HasComment("最新修改人Id");

            var modifierNameColumnName = nameRewriter.RewriteName(nameof(IBeniceSoftAudited.LastModifierName));
            b.Property(nameof(IBeniceSoftAudited.LastModifierName))
                .IsRequired(false)
                .HasColumnName(modifierNameColumnName)
                .HasComment("最新修改人姓名");

            var modificationTimeColumnName = nameRewriter.RewriteName(nameof(IBeniceSoftAudited.LastModificationTime));
            b.Property(nameof(IBeniceSoftAudited.LastModificationTime))
                .IsRequired(false)
                .HasColumnName(modificationTimeColumnName)
                .HasComment("最新修改时间");
        }
    }

    public static void TryConfigureSoftDelete(this EntityTypeBuilder b, INameRewriter nameRewriter)
    {
        if (b.Metadata.ClrType.IsAssignableTo<IBeniceSoftFullAudited>())
        {
            b.Property(nameof(IBeniceSoftFullAudited.IsDeleted))
                .IsRequired()
                .HasDefaultValue(false)
                .HasColumnName(nameRewriter.RewriteName(nameof(IBeniceSoftFullAudited.IsDeleted)))
                .HasComment("是否已删除");

            b.Property(nameof(IBeniceSoftFullAudited.DeleterId))
                .IsRequired(false)
                .HasColumnName(nameRewriter.RewriteName(nameof(IBeniceSoftFullAudited.DeleterId)))
                .HasComment("删除人Id");

            b.Property(nameof(IBeniceSoftFullAudited.DeleterName))
                .IsRequired(false)
                .HasColumnName(nameRewriter.RewriteName(nameof(IBeniceSoftFullAudited.DeleterName)))
                .HasComment("删除人姓名");

            b.Property(nameof(IBeniceSoftFullAudited.DeletionTime))
                .IsRequired(false)
                .HasColumnName(nameRewriter.RewriteName(nameof(IBeniceSoftFullAudited.DeletionTime)))
                .HasComment("删除时间");
        }
    }

    public static void TryConfigurOwnerId(this EntityTypeBuilder b, INameRewriter nameRewriter)
    {
        if (b.Metadata.ClrType.IsAssignableTo<IHaveOwnerId>())
        {
            b.Property(nameof(IHaveOwnerId.OwnerId))
                .IsRequired()
                .HasColumnName(nameRewriter.RewriteName(nameof(IHaveOwnerId.OwnerId)))
                .HasComment("拥有者Id");
        }
    }


}


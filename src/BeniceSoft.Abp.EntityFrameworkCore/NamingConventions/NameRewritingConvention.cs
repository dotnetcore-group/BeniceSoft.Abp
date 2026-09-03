using BeniceSoft.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Text;

namespace BeniceSoft.Abp.EntityFrameworkCore;

internal class NameRewritingConvention
    : IEntityTypeAddedConvention,
    IEntityTypeAnnotationChangedConvention,
    IPropertyAddedConvention,
    IForeignKeyOwnershipChangedConvention,
    IKeyAddedConvention,
    IForeignKeyAddedConvention,
    IForeignKeyPropertiesChangedConvention,
    IIndexAddedConvention,
    IEntityTypeBaseTypeChangedConvention,
    IModelFinalizingConvention
{
    private static readonly StoreObjectType[] _storeObjectTypes
      = [StoreObjectType.Table, StoreObjectType.View, StoreObjectType.Function, StoreObjectType.SqlQuery];

    private readonly Dictionary<Type, string> _sets = [];
    private readonly INameRewriter _namingNameRewriter;

    public NameRewritingConvention(ProviderConventionSetBuilderDependencies dependencies, INameRewriter nameRewriter)
    {
        _namingNameRewriter = nameRewriter;

        var ambiguousTypes = new List<Type>();
        foreach (var set in dependencies.SetFinder.FindSets(dependencies.ContextType))
        {
            if (!_sets.TryAdd(set.Type, set.Name))
            {
                ambiguousTypes.Add(set.Type);
            }
        }

        foreach (var type in ambiguousTypes)
        {
            _sets.Remove(type);
        }
    }

    public void ProcessEntityTypeAdded(IConventionEntityTypeBuilder entityTypeBuilder, IConventionContext<IConventionEntityTypeBuilder> context)
    {
        var entityType = entityTypeBuilder.Metadata;

        // Note that the table name returned here may be the result of TableNameFromDbSetConvention which ran before us.
        if (entityType.GetTableName() is { } tableName)
        {
            entityTypeBuilder.ToTable(_namingNameRewriter.RewriteName(tableName), entityType.GetSchema());
        }

        if (entityType.GetViewNameConfigurationSource() == ConfigurationSource.Convention && entityType.GetViewName() is { } viewName)
        {
            entityTypeBuilder.ToView(_namingNameRewriter.RewriteName(viewName), entityType.GetViewSchema());
        }
    }

    private void ProcessHierarchyChange(IConventionEntityTypeBuilder entityTypeBuilder)
    {
        var newMappingStrategy = entityTypeBuilder.Metadata.GetRootType().GetMappingStrategy();

        if (newMappingStrategy == RelationalAnnotationNames.TpcMappingStrategy)
        {
            foreach (var index in entityTypeBuilder.Metadata.GetIndexes())
            {
                index.Builder.HasNoAnnotation(RelationalAnnotationNames.Name);
            }

            foreach (var foreignKey in entityTypeBuilder.Metadata.GetForeignKeys())
            {
                foreignKey.Builder.HasNoAnnotation(RelationalAnnotationNames.Name);
            }
        }

        foreach (var entityType in entityTypeBuilder.Metadata.GetDerivedTypesInclusive())
        {
            entityTypeBuilder = entityType.Builder;

            entityTypeBuilder.HasNoAnnotation(RelationalAnnotationNames.TableName);
            entityTypeBuilder.HasNoAnnotation(RelationalAnnotationNames.Schema);

            if (!(newMappingStrategy == RelationalAnnotationNames.TpcMappingStrategy && entityType.ClrType.IsAbstract()))
            {
                if (GetDefaultTableName(entityType) is { } tableName)
                {
                    entityTypeBuilder.ToTable(_namingNameRewriter.RewriteName(tableName), entityType.GetSchema());
                }

                if (entityType.GetViewNameConfigurationSource() == ConfigurationSource.Convention
                    && entityType.GetViewName() is { } viewName)
                {
                    entityTypeBuilder.ToView(_namingNameRewriter.RewriteName(viewName), entityType.GetViewSchema());
                }
            }
        }

        string GetDefaultTableName(IConventionEntityType entityType)
        {
            return !entityType.HasSharedClrType && _sets.TryGetValue(entityType.ClrType, out var setName) ? setName : entityType.GetTableName()!;
        }
    }

    public void ProcessEntityTypeBaseTypeChanged(IConventionEntityTypeBuilder entityTypeBuilder, IConventionEntityType? newBaseType, IConventionEntityType? oldBaseType, IConventionContext<IConventionEntityType> context)
    {
        ProcessHierarchyChange(entityTypeBuilder);
    }

    public void ProcessPropertyAdded(IConventionPropertyBuilder propertyBuilder, IConventionContext<IConventionPropertyBuilder> context)
    {
        RewriteColumnName(propertyBuilder);
    }

    private void ProcessOwnershipChange(IConventionForeignKey foreignKey, IConventionContext context)
    {
        var ownedEntityType = foreignKey.DeclaringEntityType;

        if (foreignKey.IsOwnership)
        {
            ownedEntityType.FindPrimaryKey()?.Builder.HasNoAnnotation(RelationalAnnotationNames.Name);

            if (ownedEntityType.IsMappedToJson())
            {
                ProcessJsonOwnedEntity(ownedEntityType, ownedEntityType.GetContainerColumnName()!);

                void ProcessJsonOwnedEntity(IConventionEntityType entityType, string containerColumnName)
                {
                    entityType.Builder.HasNoAnnotation(RelationalAnnotationNames.TableName);
                    entityType.Builder.HasNoAnnotation(RelationalAnnotationNames.Schema);

                    if (containerColumnName.IsNotNull())
                    {
                        entityType.SetContainerColumnName(_namingNameRewriter.RewriteName(containerColumnName));
                    }

                    // TODO: Note that we do not rewrite names of JSON properties (which aren't relational columns).
                    // TODO: We could introduce an option for doing so, though that's probably not usually what people want when doing JSON
                    foreach (var property in entityType.GetProperties())
                    {
                        property.Builder.HasNoAnnotation(RelationalAnnotationNames.ColumnName);
                    }

                    foreach (var navigation in entityType.GetNavigations().Where(n => !n.IsOnDependent && n.ForeignKey.IsOwnership))
                    {
                        ProcessJsonOwnedEntity(navigation.TargetEntityType, containerColumnName);
                    }
                }
            }
            else
            {
                if (foreignKey.IsUnique)
                {
                    ownedEntityType.Builder.HasNoAnnotation(RelationalAnnotationNames.TableName);
                    ownedEntityType.Builder.HasNoAnnotation(RelationalAnnotationNames.Schema);

                    foreach (var property in ownedEntityType.GetProperties())
                    {
                        RewriteColumnName(property.Builder);
                    }
                }
            }

            context.StopProcessing();
        }
    }

    public void ProcessForeignKeyOwnershipChanged(IConventionForeignKeyBuilder relationshipBuilder, IConventionContext<bool?> context)
    {
        ProcessOwnershipChange(relationshipBuilder.Metadata, context);
    }

    public void ProcessEntityTypeAnnotationChanged(IConventionEntityTypeBuilder entityTypeBuilder, string name, IConventionAnnotation? annotation, IConventionAnnotation? oldAnnotation, IConventionContext<IConventionAnnotation> context)
    {
        var entityType = entityTypeBuilder.Metadata;

        switch (name)
        {
            case RelationalAnnotationNames.MappingStrategy:
                {
                    ProcessHierarchyChange(entityTypeBuilder);
                    return;
                }

            case RelationalAnnotationNames.ContainerColumnName:
                {
                    var foreignKey = entityTypeBuilder.Metadata.FindOwnership();
                    if (foreignKey != null)
                    {
                        ProcessOwnershipChange(foreignKey, context);
                    }
                    return;
                }

            case RelationalAnnotationNames.ViewName or RelationalAnnotationNames.SqlQuery or RelationalAnnotationNames.FunctionName when annotation?.Value is not null && entityType.GetTableNameConfigurationSource() == ConfigurationSource.Convention:
                {
                    entityType.SetTableName(null);
                    return;
                }

            case RelationalAnnotationNames.TableName
                when StoreObjectIdentifier.Create(entityType, StoreObjectType.Table) is StoreObjectIdentifier tableIdentifier:
                {
                    var mappingStrategy = entityType.GetMappingStrategy();

                    if (entityType.FindPrimaryKey() is IConventionKey primaryKey)
                    {
                        var rootType = entityType.GetRootType();
                        var isTPT = rootType.GetDerivedTypes().FirstOrDefault() is { } derivedType
                            && derivedType.GetTableName() != rootType.GetTableName();

                        if (entityType.FindRowInternalForeignKeys(tableIdentifier).FirstOrDefault() is null && !isTPT)
                        {
                            if (primaryKey.GetDefaultName() is { } primaryKeyName)
                            {
                                primaryKey.Builder.HasName(_namingNameRewriter.RewriteName(primaryKeyName));
                            }
                        }
                        else
                        {
                            foreach (var type in entityType.GetRootType().GetDerivedTypesInclusive())
                            {
                                if (type.FindPrimaryKey() is IConventionKey pk)
                                {
                                    pk.Builder.HasNoAnnotation(RelationalAnnotationNames.Name);
                                }
                            }
                        }
                    }

                    foreach (var foreignKey in entityType.GetDeclaredForeignKeys())
                    {
                        // See note in ProcessHierarchyChange on indexes and foreign keys in TPC hierarchies
                        if (mappingStrategy == RelationalAnnotationNames.TpcMappingStrategy && entityType.GetDerivedTypes().Any())
                        {
                            foreignKey.Builder.HasNoAnnotation(RelationalAnnotationNames.Name);
                        }
                        else if (foreignKey.GetDefaultName() is { } foreignKeyName)
                        {
                            foreignKey.Builder.HasConstraintName(_namingNameRewriter.RewriteName(foreignKeyName));
                        }
                    }

                    foreach (var index in entityType.GetDeclaredIndexes())
                    {
                        // See note in ProcessHierarchyChange on indexes and foreign keys in TPC hierarchies
                        if (mappingStrategy == RelationalAnnotationNames.TpcMappingStrategy && entityType.GetDerivedTypes().Any())
                        {
                            index.Builder.HasNoAnnotation(RelationalAnnotationNames.TableName);
                        }
                        else if (index.GetDefaultDatabaseName() is { } indexName)
                        {
                            index.Builder.HasDatabaseName(_namingNameRewriter.RewriteName(indexName));
                        }
                    }

                    if (annotation?.Value is not null && entityType.FindOwnership() is IConventionForeignKey ownership && (string)annotation.Value != ownership.PrincipalEntityType.GetTableName())
                    {
                        foreach (var property in entityType.GetProperties().Except(entityType.FindPrimaryKey()?.Properties ?? []).Where(p => p.Builder.CanSetColumnName(null)))
                        {
                            RewriteColumnName(property.Builder);
                        }

                        if (entityType.FindPrimaryKey() is IConventionKey key
                            && key.GetDefaultName() is { } keyName)
                        {
                            key.Builder.HasName(_namingNameRewriter.RewriteName(keyName));
                        }
                    }

                    return;
                }
        }
    }

    public void ProcessForeignKeyAdded(IConventionForeignKeyBuilder foreignKeyBuilder, IConventionContext<IConventionForeignKeyBuilder> context)
    {
        if (foreignKeyBuilder.Metadata.GetDefaultName() is { } constraintName)
        {
            foreignKeyBuilder.HasConstraintName(_namingNameRewriter.RewriteName(constraintName));
        }
    }

    public void ProcessForeignKeyPropertiesChanged(IConventionForeignKeyBuilder relationshipBuilder, IReadOnlyList<IConventionProperty> oldDependentProperties, IConventionKey oldPrincipalKey, IConventionContext<IReadOnlyList<IConventionProperty>> context)
    {
        if (relationshipBuilder.Metadata.GetDefaultName() is { } constraintName && relationshipBuilder.Metadata.IsInModel)
        {
            relationshipBuilder.HasConstraintName(_namingNameRewriter.RewriteName(constraintName));
        }
    }

    public void ProcessKeyAdded(IConventionKeyBuilder keyBuilder, IConventionContext<IConventionKeyBuilder> context)
    {
        if (keyBuilder.Metadata.GetName() is { } keyName)
        {
            keyBuilder.HasName(_namingNameRewriter.RewriteName(keyName));
        }
    }

    public void ProcessIndexAdded(IConventionIndexBuilder indexBuilder, IConventionContext<IConventionIndexBuilder> context)
    {
        if (indexBuilder.Metadata.GetDefaultDatabaseName() is { } indexName)
        {
            indexBuilder.HasDatabaseName(_namingNameRewriter.RewriteName(indexName));
        }
    }

    /// <summary>
    /// EF Core's <see cref="SharedTableConvention" /> runs at model finalization time, and adds entity type prefixes to
    /// clashing columns. These prefixes also needs to be rewritten by us, so we run after that convention to do that.
    /// </summary>
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (entityType.GetTableName() != null && _sets.ContainsKey(entityType.ClrType))
            {
                switch (entityType.GetMappingStrategy())
                {
                    case RelationalAnnotationNames.TpcMappingStrategy when entityType.IsAbstract():
                    case RelationalAnnotationNames.TphMappingStrategy when entityType.BaseType is not null:
                        entityType.Builder.HasNoAnnotation(RelationalAnnotationNames.TableName);
                        break;
                }
            }

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName();
                if (columnName.StartsWith(entityType.ShortName() + '_', StringComparison.Ordinal))
                {
                    property.Builder.HasColumnName(string.Concat(_namingNameRewriter.RewriteName(entityType.ShortName()), columnName.AsSpan(entityType.ShortName().Length)));
                }

                var storeObject = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table);
                if (storeObject is null)
                {
                    continue;
                }

                var shortName = entityType.ShortName();

                if (property.Builder.CanSetColumnName(null))
                {
                    columnName = property.GetColumnName();
                    if (columnName.StartsWith(shortName + '_', StringComparison.Ordinal))
                    {
                        property.Builder.HasColumnName(string.Concat(_namingNameRewriter.RewriteName(shortName)
, columnName.AsSpan(shortName.Length)));
                    }
                }

                if (property.Builder.CanSetColumnName(null, storeObject.Value))
                {
                    columnName = property.GetColumnName(storeObject.Value);
                    if (columnName is { } colName && colName.StartsWith(shortName + '_', StringComparison.Ordinal))
                    {
                        property.Builder.HasColumnName(string.Concat(_namingNameRewriter.RewriteName(shortName), columnName.AsSpan(shortName.Length)),
                            storeObject.Value);
                    }
                }
            }
        }
    }

    private void RewriteColumnName(IConventionPropertyBuilder propertyBuilder)
    {
        var property = propertyBuilder.Metadata;
        var structuralType = property.DeclaringType;

        property.Builder.HasNoAnnotation(RelationalAnnotationNames.ColumnName);

        var baseColumnName = StoreObjectIdentifier.Create(structuralType, StoreObjectType.Table) is { } tableIdentifier ? property.GetDefaultColumnName(tableIdentifier) : property.GetDefaultColumnName();
        if (baseColumnName is not null)
        {
            propertyBuilder.HasColumnName(_namingNameRewriter.RewriteName(baseColumnName));
        }

        foreach (var storeObjectType in _storeObjectTypes)
        {
            var identifier = StoreObjectIdentifier.Create(structuralType, storeObjectType);
            if (identifier is null)
            {
                continue;
            }

            if (property.GetColumnNameConfigurationSource(identifier.Value) == ConfigurationSource.Convention && property.GetColumnName(identifier.Value) is { } columnName)
            {
                propertyBuilder.HasColumnName(_namingNameRewriter.RewriteName(columnName), identifier.Value);
            }
        }
    }
}

file sealed class NamingConventionSetPlugin(ProviderConventionSetBuilderDependencies dependencies, IDbContextOptions options) : IConventionSetPlugin
{
    public ConventionSet ModifyConventions(ConventionSet conventionSet)
    {
        var extension = options.FindExtension<NamingConventionsOptionsExtension>();
        var namingStyle = extension?.NamingConvention;
        var culture = extension?.Culture;
        if (namingStyle is null || namingStyle == NamingConvention.None)
        {
            return conventionSet;
        }

        INameRewriter nameRewriter = namingStyle switch
        {
            NamingConvention.SnakeCase => new SnakeCaseNameRewriter(culture ?? CultureInfo.InvariantCulture),
            NamingConvention.LowerCase => new LowerCaseNameRewriter(culture ?? CultureInfo.InvariantCulture),
            NamingConvention.CamelCase => new CamelCaseNameRewriter(culture ?? CultureInfo.InvariantCulture),
            NamingConvention.UpperCase => new UpperCaseNameRewriter(culture ?? CultureInfo.InvariantCulture),
            NamingConvention.UpperSnakeCase => new UpperSnakeCaseNameRewriter(culture ?? CultureInfo.InvariantCulture),
            NamingConvention.None => throw new NotImplementedException(),
            _ => new EmptyNameRewriter()
        };

        var convention = new NameRewritingConvention(dependencies, nameRewriter);

        conventionSet.EntityTypeAddedConventions.Add(convention);
        conventionSet.EntityTypeAnnotationChangedConventions.Add(convention);
        conventionSet.PropertyAddedConventions.Add(convention);
        conventionSet.ForeignKeyOwnershipChangedConventions.Add(convention);
        conventionSet.KeyAddedConventions.Add(convention);
        conventionSet.ForeignKeyAddedConventions.Add(convention);
        conventionSet.ForeignKeyPropertiesChangedConventions.Add(convention);
        conventionSet.IndexAddedConventions.Add(convention);
        conventionSet.EntityTypeBaseTypeChangedConventions.Add(convention);
        conventionSet.ModelFinalizingConventions.Add(convention);

        return conventionSet;
    }
}

internal sealed class NamingConventionsOptionsExtension : IDbContextOptionsExtension
{
    public NamingConventionsOptionsExtension(NamingConvention namingConvention, CultureInfo? culture = null)
    {
        Culture = culture;
        NamingConvention = namingConvention;
        Info = new ExtensionInfo(this);
    }

    public DbContextOptionsExtensionInfo Info { get; }

    internal NamingConvention NamingConvention { get; private set; }

    internal CultureInfo? Culture { get; private set; }

    public void Validate(IDbContextOptions options)
    {
    }

    public void ApplyServices(IServiceCollection services)
    {
        new EntityFrameworkServicesBuilder(services).TryAdd<IConventionSetPlugin, NamingConventionSetPlugin>();
    }

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        private string _logFragment = string.Empty;

        private new NamingConventionsOptionsExtension Extension
            => (NamingConventionsOptionsExtension)base.Extension;

        public override bool IsDatabaseProvider => false;

        public override string LogFragment
        {
            get
            {
                if (_logFragment == null)
                {
                    var builder = new StringBuilder();

                    builder.Append(Extension.NamingConvention switch
                    {
                        NamingConvention.None => "using default naming",
                        NamingConvention.SnakeCase => "using snake-case naming ",
                        NamingConvention.LowerCase => "using lower case naming",
                        NamingConvention.UpperCase => "using upper case naming",
                        NamingConvention.UpperSnakeCase => "using upper snake-case naming",
                        NamingConvention.CamelCase => "using camel-case naming",
                        _ => "Unhandled enum value: " + Extension.NamingConvention
                    });

                    if (Extension.Culture is null)
                    {
                        builder.Append(" (culture=").Append(Extension.Culture).Append(')');
                    }

                    _logFragment = builder.ToString();
                }

                return _logFragment;
            }
        }

        public override int GetServiceProviderHashCode()
        {
            var hashCode = Extension.NamingConvention.GetHashCode();
            hashCode = (hashCode * 3) ^ (Extension.Culture?.GetHashCode() ?? 0);
            return hashCode;
        }

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
        {
            return other is ExtensionInfo;
        }

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
            debugInfo["Naming:UseNamingConvention"] = Extension.NamingConvention.GetHashCode().ToString(CultureInfo.InvariantCulture);
            if (Extension.Culture != null)
            {
                debugInfo["Naming:Culture"]
                    = Extension.Culture.GetHashCode().ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}

public enum NamingConvention
{
    None,

    /// <summary>
    /// 蛇形命名法 / 下划线命名法  user_name
    /// </summary>
    SnakeCase,

    /// <summary>
    /// 小写命名法 username
    /// </summary>
    LowerCase,

    /// <summary>
    /// 驼峰命名法  userName
    /// </summary>
    CamelCase,

    /// <summary>
    /// 大写命名法  USERNAME
    /// </summary>
    UpperCase,

    /// <summary>
    /// 大写蛇形命名法 / 常量命名法  USER_NAME
    /// </summary>
    UpperSnakeCase
}
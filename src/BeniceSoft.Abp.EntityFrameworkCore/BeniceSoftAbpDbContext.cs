using BeniceSoft.Abp.Core.Users;
using BeniceSoft.Abp.Ddd.Domain.Entity;
using BeniceSoft.Abp.Extensions.AuditTrail.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Options;
using System.Globalization;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore;

public abstract class BeniceSoftAbpDbContext<TDbContext> : AbpDbContext<TDbContext>
    where TDbContext : DbContext
{
    protected IBeniceSoftCurrentUser? CurrentUser => LazyServiceProvider?.LazyGetService<IBeniceSoftCurrentUser>();
    protected IEntityChangeDispatcher? EntityChangeDispatcher => LazyServiceProvider?.LazyGetService<IEntityChangeDispatcher>();
    protected BeniceSoftAbpAuditTrailOptions? AuditTrailOptions => LazyServiceProvider?.LazyGetService<IOptions<BeniceSoftAbpAuditTrailOptions>>()?.Value;

    /// <summary>
    /// 数据库命名规范，默认为None（与实体属性名称一致）
    /// 子类可以重写此属性来使用其他命名规范
    /// </summary>
    protected virtual NamingConvention NamingConvention => NamingConvention.None;

    protected virtual List<string> IgnoreTableNames()
    {
        return [nameof(ExtraPropertyDictionary)];
    }

    protected BeniceSoftAbpDbContext(DbContextOptions<TDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.UseNamingConvention(NamingConvention);
        optionsBuilder.UseNoRelation();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        OnBeniceSoftModelCreating(modelBuilder);

        GlobalConfigureBeniceSoftAuditedProperties(modelBuilder);
    }

    /// <summary>
    /// 全局配置自定义审计字段索引和命名规范
    /// </summary>
    /// <param name="modelBuilder"></param>
    protected virtual void GlobalConfigureBeniceSoftAuditedProperties(ModelBuilder modelBuilder)
    {
        var entityTypes = modelBuilder.Model.GetEntityTypes()
                .Where(a => !IgnoreTableNames().Contains(a.ClrType.Name))
                .Where(a => !a.IsOwned());

        var nameRewriter = GetNameRewriter(NamingConvention, CultureInfo.InvariantCulture);
        foreach (var entityType in entityTypes)
        {
            var builder = modelBuilder.Entity(entityType.ClrType);
            builder.ConfigureBeniceSoftConventions(nameRewriter);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyBeniceSoftAuditConcepts();

        var dispatcher = EntityChangeDispatcher;
        var auditChanges = dispatcher is not null ? CaptureAuditTrailChanges() : [];

        var result = base.SaveChanges(acceptAllChangesOnSuccess);

        if (auditChanges.Count > 0)
        {
            dispatcher!.DispatchAsync(auditChanges).GetAwaiter().GetResult();
        }

        return result;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyBeniceSoftAuditConcepts();

        var dispatcher = EntityChangeDispatcher;
        var auditChanges = dispatcher is not null ? CaptureAuditTrailChanges() : [];

        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        if (auditChanges.Count > 0)
        {
            await dispatcher!.DispatchAsync(auditChanges);
        }

        return result;
    }

    /// <summary>
    /// 采集标记了 [AuditTracked] 的属性变更
    /// 子类可重写此方法来自定义采集逻辑或禁用采集
    /// </summary>
    protected virtual List<EntityChangeRecord> CaptureAuditTrailChanges()
    {
        var options = AuditTrailOptions;

        if (options is null || !options.Enabled)
        {
            return [];
        }

        return AuditTrailChangeTracker.CaptureChanges(
            ChangeTracker,
            CurrentUser?.Id,
            CurrentUser?.Name,
            options.ExcludedEntityTypes);
    }

    protected virtual void ApplyBeniceSoftAuditConcepts()
    {
        var userId = CurrentUser?.Id;
        var userName = CurrentUser?.Name;

        foreach (var entry in ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    SetCreationAuditProperties(entry, userId, userName);
                    SetOwnerIdIfNeeded(entry, userId);
                    break;
                case EntityState.Modified:
                    if (IsMarkingEntityAsDeletedViaUpdate(entry))
                    {
                        SetDeletionAuditProperties(entry, userId, userName);
                    }
                    else
                    {
                        SetModificationAuditProperties(entry, userId, userName);
                    }
                    break;
                case EntityState.Deleted:
                    SetDeletionAuditProperties(entry, userId, userName);
                    break;
            }
        }
    }

    /// <summary>
    /// 检查是否是软删除操作
    /// </summary>
    protected virtual bool IsMarkingEntityAsDeletedViaUpdate(EntityEntry entry)
    {
        if (entry.Entity is not ISoftDelete)
        {
            return false;
        }

        var isDeletedProperty = entry.Property(nameof(ISoftDelete.IsDeleted));
        return isDeletedProperty.IsModified
               && isDeletedProperty.OriginalValue is false
               && isDeletedProperty.CurrentValue is true;
    }

    protected virtual void SetOwnerIdIfNeeded(EntityEntry entry, long? userId)
    {
        if (entry.Entity is not IHaveOwnerId)
        {
            return;
        }

        var ownerIdProperty = entry.Property(nameof(IHaveOwnerId.OwnerId));
        if (ownerIdProperty.CurrentValue is long currentValue && currentValue != 0)
        {
            return;
        }

        if (userId.HasValue)
        {
            ownerIdProperty.CurrentValue = userId.Value;
        }
    }

    protected virtual void SetCreationAuditProperties(EntityEntry entry, long? userId, string? userName)
    {
        if (entry.Entity is IBeniceSoftAudited entity)
        {
            if (entity.CreationTime == default)
            {
                entry.Property(nameof(IBeniceSoftAudited.CreationTime)).CurrentValue = DateTimeOffset.UtcNow;
            }

            if (entity.CreatorId == default && userId.HasValue)
            {
                entry.Property(nameof(IBeniceSoftAudited.CreatorId)).CurrentValue = userId.Value;
            }

            if (string.IsNullOrWhiteSpace(entity.CreatorName) && !string.IsNullOrWhiteSpace(userName))
            {
                entry.Property(nameof(IBeniceSoftAudited.CreatorName)).CurrentValue = userName;
            }
        }
    }

    protected virtual void SetModificationAuditProperties(EntityEntry entry, long? userId, string? userName)
    {
        if (entry.Entity is IBeniceSoftAudited entity)
        {
            entry.Property(nameof(IBeniceSoftAudited.LastModificationTime)).CurrentValue = DateTimeOffset.UtcNow;

            if (userId.HasValue)
            {
                entry.Property(nameof(IBeniceSoftAudited.LastModifierId)).CurrentValue = userId.Value;
            }

            if (!string.IsNullOrWhiteSpace(userName))
            {
                entry.Property(nameof(IBeniceSoftAudited.LastModifierName)).CurrentValue = userName;
            }
        }
    }

    protected virtual void SetDeletionAuditProperties(EntityEntry entry, long? userId, string? userName)
    {
        if (entry.Entity is not IBeniceSoftFullAudited)
        {
            return;
        }

        if (entry.State == EntityState.Deleted)
        {
            entry.State = EntityState.Modified;
            entry.Property(nameof(ISoftDelete.IsDeleted)).CurrentValue = true;
        }

        entry.Property(nameof(IBeniceSoftFullAudited.DeletionTime)).CurrentValue = DateTimeOffset.UtcNow;

        if (userId.HasValue)
        {
            entry.Property(nameof(IBeniceSoftFullAudited.DeleterId)).CurrentValue = userId.Value;
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            entry.Property(nameof(IBeniceSoftFullAudited.DeleterName)).CurrentValue = userName;
        }
    }

    protected abstract void OnBeniceSoftModelCreating(ModelBuilder modelBuilder);

    private static INameRewriter GetNameRewriter(NamingConvention namingConvention, CultureInfo culture)
    {
        return namingConvention switch
        {
            NamingConvention.SnakeCase => new SnakeCaseNameRewriter(culture),
            NamingConvention.LowerCase => new LowerCaseNameRewriter(culture),
            NamingConvention.CamelCase => new CamelCaseNameRewriter(culture),
            NamingConvention.UpperCase => new UpperCaseNameRewriter(culture),
            NamingConvention.UpperSnakeCase => new UpperSnakeCaseNameRewriter(culture),
            _ => new EmptyNameRewriter()
        };
    }
}


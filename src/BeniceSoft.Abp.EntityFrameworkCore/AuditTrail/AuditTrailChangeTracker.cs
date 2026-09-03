using BeniceSoft.Abp.Ddd.Domain.Entity;
using BeniceSoft.Abp.Extensions.AuditTrail.Abstractions;
using BeniceSoft.Core.Reflector;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections.Concurrent;
using System.Reflection;

namespace BeniceSoft.Abp.EntityFrameworkCore;

/// <summary>
/// 从 EF Core ChangeTracker 中采集标记了 [AuditTracked] 的属性变更
/// </summary>
public static class AuditTrailChangeTracker
{
    /// <summary>
    /// 缓存每个实体类型中标记了 [AuditTracked] 的属性信息
    /// Key: 实体类型
    /// Value: 属性名 -> (PropertyInfo, DisplayName)
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Dictionary<string, (PropertyInfo PropInfo, string DisplayName)>>
        TrackedPropertiesCache = new();

    /// <summary>
    /// 获取实体类型中所有标记了 [AuditTracked] 的属性
    /// </summary>
    private static Dictionary<string, (PropertyInfo PropInfo, string DisplayName)> GetTrackedProperties(Type entityType)
    {
        return TrackedPropertiesCache.GetOrAdd(entityType, type =>
        {
            var result = new Dictionary<string, (PropertyInfo, string)>();
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetReflector().GetCustomAttribute<AuditTrackedAttribute>();
                if (attr is not null)
                {
                    var displayName = attr.DisplayName ?? prop.Name;
                    result[prop.Name] = (prop, displayName);
                }
            }

            return result;
        });
    }

    /// <summary>
    /// 从 ChangeTracker 中采集变更记录
    /// 必须在 SaveChanges 之前调用
    /// </summary>
    public static List<EntityChangeRecord> CaptureChanges(
        ChangeTracker changeTracker,
        long? operatorId,
        string? operatorName,
        HashSet<string>? excludedEntityTypes = null)
    {
        var records = new List<EntityChangeRecord>();

        foreach (var entry in changeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var entityType = entry.Entity.GetType();

            // 排除配置中指定的实体类型
            if (excludedEntityTypes is { Count: > 0 } && excludedEntityTypes.Contains(entityType.Name))
            {
                continue;
            }
            var trackedProps = GetTrackedProperties(entityType);

            if (trackedProps.Count == 0)
            {
                continue;
            }

            var changes = new List<PropertyChangeInfo>();

            foreach (var prop in entry.Properties)
            {
                var propName = prop.Metadata.Name;
                if (!trackedProps.TryGetValue(propName, out var tracked))
                {
                    continue;
                }

                switch (entry.State)
                {
                    case EntityState.Added:
                        changes.Add(new PropertyChangeInfo
                        {
                            PropertyName = propName,
                            DisplayName = tracked.DisplayName,
                            OriginalValue = null,
                            NewValue = prop.CurrentValue?.ToString()
                        });
                        break;

                    case EntityState.Modified when prop.IsModified:
                        changes.Add(new PropertyChangeInfo
                        {
                            PropertyName = propName,
                            DisplayName = tracked.DisplayName,
                            OriginalValue = prop.OriginalValue?.ToString(),
                            NewValue = prop.CurrentValue?.ToString()
                        });
                        break;

                    case EntityState.Deleted:
                        changes.Add(new PropertyChangeInfo
                        {
                            PropertyName = propName,
                            DisplayName = tracked.DisplayName,
                            OriginalValue = prop.OriginalValue?.ToString(),
                            NewValue = null
                        });
                        break;
                }
            }

            if (changes.Count == 0)
            {
                continue;
            }

            // 尝试获取实体 Id
            var idProperty = entry.Properties.FirstOrDefault(p =>
                string.Equals(p.Metadata.Name, "Id", StringComparison.OrdinalIgnoreCase));

            records.Add(new EntityChangeRecord
            {
                ChangeTime = DateTimeOffset.UtcNow,
                EntityType = entityType.Name,
                EntityId = idProperty?.CurrentValue?.ToString(),
                ChangeType = entry.State.ToString(),
                OperatorId = operatorId,
                OperatorName = operatorName,
                Changes = changes
            });
        }

        return records;
    }
}


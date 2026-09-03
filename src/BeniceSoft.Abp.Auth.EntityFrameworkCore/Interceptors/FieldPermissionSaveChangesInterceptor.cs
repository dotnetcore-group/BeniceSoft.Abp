using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using BeniceSoft.Abp.Auth.Core;
using BeniceSoft.Abp.Auth.Core.Models;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore.Interceptors;

/// <summary>
/// 字段权限保存拦截器
/// 在保存前检查用户是否拥有字段的写权限，如果没有则忽略该字段的修改
/// </summary>
public class FieldPermissionSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserPermissionAccessor _permissionAccessor;

    public FieldPermissionSaveChangesInterceptor(ICurrentUserPermissionAccessor permissionAccessor)
    {
        _permissionAccessor = permissionAccessor;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyFieldPermissionFilter(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyFieldPermissionFilter(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyFieldPermissionFilter(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var fieldPermissions = _permissionAccessor.UserPermission?.FieldPermissions;
        if (fieldPermissions is null or { Count: 0 })
        {
            return;
        }

        // 按表名分组缓存转字典，提升查找性能
        var permissionsByTable = fieldPermissions
            .GroupBy(c => c.TableName)
            .ToDictionary(g => g.Key, g => g.ToDictionary(c => c.FieldName, c => c));

        var modifiedEntries = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in modifiedEntries)
        {
            var tableName = entry.Metadata.GetTableName();
            if (string.IsNullOrEmpty(tableName))
            {
                continue;
            }

            if (!permissionsByTable.TryGetValue(tableName, out var fieldPermissionDict))
            {
                continue;
            }

            foreach (var property in entry.Metadata.GetProperties())
            {
                if (!fieldPermissionDict.TryGetValue(property.Name, out var fieldPermission))
                {
                    continue;
                }

                if (!HasWritePermission(fieldPermission.FieldAuthLevel))
                {
                    var propertyEntry = entry.Property(property.Name);
                    propertyEntry.IsModified = false;
                }
            }
        }
    }

    /// <summary>
    /// 判断是否有写权限
    /// FieldAuthLevel 使用位运算：1=无权限，2=只读，4=读写
    /// </summary>
    private static bool HasWritePermission(int authLevel)
    {
        return (authLevel & (int)FieldAuthLevelEnum.ReadWrite) != 0;
    }
}

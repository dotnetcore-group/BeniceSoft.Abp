using BeniceSoft.Core;
using BeniceSoft.Core.Strategy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Abp.EntityFrameworkCore;

public static class EFCoreExtensions
{
    #region DbContext

    public static IServiceProvider? GetApplicationServiceProvider(this DbContext ctx)
        => ctx.GetService<IDbContextOptions>()
            ?.FindExtension<CoreOptionsExtension>()
            ?.ApplicationServiceProvider;

    public static T? Resolve<T>(this DbContext ctx, object? serviceKey = null)
    {
        var serviceProvider = ctx.GetApplicationServiceProvider()
            ?? throw new InvalidOperationException("ApplicationServiceProvider is not available on this DbContext.");

        return serviceKey == null
            ? serviceProvider.GetService<T>()
            : serviceProvider.GetKeyedService<T>(serviceKey);
    }

    public static object? Resolve(this DbContext ctx, Type serviceType, object? serviceKey = null)
    {
        var serviceProvider = ctx.GetApplicationServiceProvider()
            ?? throw new InvalidOperationException("ApplicationServiceProvider is not available on this DbContext.");

        return serviceKey == null
            ? serviceProvider.GetService(serviceType)
            : serviceProvider.GetKeyedService(serviceType, serviceKey);
    }

    #endregion

    #region ModelBuilder
    /// <summary>
    /// 不生成外键关系
    /// </summary>
    /// <param name="optionsBuilder"></param>
    /// <returns></returns>
    public static DbContextOptionsBuilder UseNoRelation(this DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ReplaceService<IMigrationsModelDiffer, NoRelationMigrationsModelDiffer>();
        return optionsBuilder;
    }

    /// <summary>
    /// 大小写命名规范
    /// doc to https://github.com/efcore/EFCore.NamingConventions
    /// </summary>
    /// <param name="optionsBuilder"></param>
    /// <param name="convention"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    public static DbContextOptionsBuilder UseNamingConvention(this DbContextOptionsBuilder optionsBuilder, NamingConvention convention, CultureInfo? culture = null)
    {
        var extension = new NamingConventionsOptionsExtension(convention, culture);
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        return optionsBuilder;
    }

    public static void SetEntities<T>(this ModelBuilder builder, params Assembly[] assemblies)
    {
        var types = TypeUtils.FindClassesOfType<T>(assemblies);
        foreach (var type in types)
        {
            if (type.IsDefined<NotMappedAttribute>())
            {
                continue;
            }

            builder.Entity(type);
        }
    }

    public static void SetEntities<T>(this ModelBuilder builder, params string[] assemblyNames)
    {
        builder.SetEntities<T>(TypeUtils.GetAssemblies(assemblyNames).ToArray());
    }

    public static PropertyBuilder<byte[]> ShadowRowVersion<T>(this ModelBuilder builder, string columnName = "Version")
        where T : class
    {
        return builder.Entity<T>().ShadowRowVersion(columnName);
    }

    public static PropertyBuilder<byte[]> ShadowRowVersion<T>(this EntityTypeBuilder<T> builder, string columnName = "Version")
        where T : class
    {
        return builder.Property<byte[]>(columnName).IsRowVersion().HasColumnName(columnName);
    }

    public static EntityTypeBuilder<T> HasJsonColumn<T, TO>(this EntityTypeBuilder<T> builder, Expression<Func<T, TO?>> navigation, Action<OwnedNavigationBuilder<T, TO>>? buildAction = null)
        where T : class
        where TO : class
    {
        builder.OwnsOne(navigation, b =>
        {
            b.ToJson();
            buildAction?.Invoke(b);
        });

        return builder;
    }

    public static PropertyBuilder<string> HasValueGenerator<TG>(this PropertyBuilder<string> builder, string prefix)
        where TG : IIdGenerator
    {
        var id = Singleton<TG>.Instance;
        if (id == null)
        {
            throw new ArgumentException("IdGenerator instance not set");
        }

        builder.HasValueGenerator((_, _) => new IdValueGenerator(id, prefix));
        return builder;
    }

    public static PropertyBuilder<long> HasValueGenerator<TG>(this PropertyBuilder<long> builder)
        where TG : IIdGenerator
    {
        var id = Singleton<TG>.Instance;
        if (id == null)
        {
            throw new ArgumentException("IdGenerator instance not set");
        }

        builder.HasValueGenerator((_, _) => new Int64ValueGenerator(id));
        return builder;
    }

    public static PropertyBuilder<T> HasReadOnly<T>(this PropertyBuilder<T> builder, bool readOnly = true)
    {
        builder.HasAnnotation("KD-RO", readOnly ? "1" : "0");
        return builder;
    }
    #endregion

    #region Queryable

    /// <summary>
    /// convert to datatable  
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static DataTable ToDataTable<T>(this IEnumerable<T> aim)
        where T : class
    {
        var list = aim;
        var type = typeof(T);
        var name = type.Name;
        var tableAttr = typeof(T).GetCustomAttribute<TableAttribute>();
        if (tableAttr != null && tableAttr.Name.IsNotNull())
        {
            name = tableAttr.Name;
        }

        var dt = new DataTable(name);

        //get the aim object type
        var plist = type.GetProperties().FindAll(t => !t.IsDefined<NotMappedAttribute>());
        var columns = plist.ToDictionary(p => p, p =>
        {
            var column = p.GetCustomAttribute<ColumnAttribute>();
            if (column != null && column.Name != null && column.Name.IsNotNull())
            {
                return column.Name;
            }

            return p.Name;
        });

        //add property name to columns
        foreach (var proper in plist)
        {
            var colType = proper.PropertyType;
            colType = colType.GetUnderlyingType();
            dt.Columns.Add(columns[proper], colType);
        }

        foreach (var item in list)
        {
            var dr = dt.NewRow();
            //fill data to datarow
            foreach (var proper in plist)
            {
                if (!proper.CanRead)
                {
                    continue;
                }

                dr[columns[proper]] = proper.GetValue(item) ?? DBNull.Value;
                Thread.Yield();
            }

            dt.Rows.Add(dr);
        }

        dt.AcceptChanges();
        return dt;
    }

    /// <summary>
    /// convert to list
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static List<T> ToList<T>(this DataTable aim)
        where T : class, new()
    {
        if (aim == null) return [];

        var props = typeof(T).GetProperties()
            .Where(t => t.CanWrite && !t.IsDefined<NotMappedAttribute>())
            .Select(p =>
            {
                var col = p.GetCustomAttribute<ColumnAttribute>();
                var colName = col?.Name ?? p.Name;
                return new { Prop = p, ColName = colName, ColIndex = aim.Columns.IndexOf(colName) };
            })
            .Where(x => x.ColIndex >= 0)
            .ToList();

        var list = new List<T>(aim.Rows.Count);

        foreach (DataRow row in aim.Rows)
        {
            var t = new T();
            foreach (var item in props)
            {
                var val = row[item.ColIndex];
                if (val == DBNull.Value) continue;

                try
                {
                    var targetType = Nullable.GetUnderlyingType(item.Prop.PropertyType) ?? item.Prop.PropertyType;
                    if (val is string str && string.IsNullOrWhiteSpace(str) && targetType != typeof(string))
                    {
                        if (Nullable.GetUnderlyingType(item.Prop.PropertyType) != null)
                            item.Prop.SetValue(t, null);

                        continue;
                    }

                    object convertedValue;

                    if (targetType == typeof(DateTimeOffset) || targetType == typeof(DateTimeOffset?))
                    {
                        convertedValue = ParseDateTimeOffset(val);
                    }
                    else if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
                    {
                        convertedValue = ParseDateTime(val);
                    }
                    else if (targetType == typeof(TimeSpan) || targetType == typeof(TimeSpan?))
                    {
                        convertedValue = ParseTimeSpan(val);
                    }
                    else if (targetType.IsEnum)
                    {
                        convertedValue = val is string s
                            ? Enum.Parse(targetType, s, ignoreCase: true)
                            : Enum.ToObject(targetType, val);
                    }
                    else
                    {
                        convertedValue = Convert.ChangeType(val, targetType);
                    }

                    item.Prop.SetValue(t, convertedValue);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"第 {aim.Rows.IndexOf(row) + 1} 行，列 '{item.ColName}' → 属性 '{item.Prop.Name}' " +
                        $"(目标类型: {item.Prop.PropertyType.Name}, 实际值: '{val}', 实际类型: {val?.GetType().Name ?? "null"}) 转换失败。",
                        ex);
                }
            }
            list.Add(t);
        }
        return list;
    }

    // 解析 DateTimeOffset，支持多种常见格式
    private static DateTimeOffset ParseDateTimeOffset(object val)
    {
        if (val is DateTimeOffset dto) return dto;
        if (val is DateTime dt) return new DateTimeOffset(dt);

        var s = val.ToStringSafe().Trim();

        if (DateTimeOffset.TryParse(s, out var result))
            return result;

        var formats = new[]
        {
        "MM/dd/yyyy",
        "MM/dd/yyyy HH:mm:ss",
        "M/d/yyyy",
        "M/d/yyyy H:mm:ss",
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy/MM/dd",
        "dd/MM/yyyy",
        "dd/MM/yyyy HH:mm:ss",
        "yyyyMMdd",
        "yyyyMMddHHmmss",
        "O", // ISO 8601
        "s", // Sortable
    };

        if (DateTimeOffset.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return result;

        throw new FormatException($"无法将 '{s}' 解析为 DateTimeOffset，支持的格式包括: {string.Join(", ", formats)}");
    }

    // 解析 DateTime
    private static DateTime ParseDateTime(object val)
    {
        if (val is DateTime dt) return dt;
        if (val is DateTimeOffset dto) return dto.DateTime;

        var s = val.ToStringSafe().Trim();

        if (DateTime.TryParse(s, out var result))
            return result;

        var formats = new[]
        {
        "MM/dd/yyyy",
        "MM/dd/yyyy HH:mm:ss",
        "M/d/yyyy",
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy/MM/dd",
        "dd/MM/yyyy",
        "O",
        "s",
    };

        if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return result;

        throw new FormatException($"无法将 '{s}' 解析为 DateTime");
    }

    // 解析 TimeSpan
    private static TimeSpan ParseTimeSpan(object val)
    {
        if (val is TimeSpan ts) return ts;

        var s = val.ToStringSafe().Trim();

        if (TimeSpan.TryParse(s, out var result))
            return result;

        // 支持 HH:mm:ss 格式
        if (TimeSpan.TryParseExact(s, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out result))
            return result;

        throw new FormatException($"无法将 '{s}' 解析为 TimeSpan");
    }

    #endregion
}

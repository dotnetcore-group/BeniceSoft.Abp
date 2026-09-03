using BeniceSoft.Core;
using System.ComponentModel;
using System.Reflection;

namespace BeniceSoft.Office.Excel;

[AttributeUsage(AttributeTargets.Property)]
public class ExcelColumnAttribute : Attribute
{
    #region Members
    /// <summary>
    /// 列索引
    /// </summary>
    public int Index { get; set; } = -1;

    /// <summary>
    /// 列名
    /// </summary>
    public string? Name { get; internal set; }

    /// <summary>
    /// 属性名,用于动态类型（dynamic type）
    /// </summary>
    public string? PropertyName { get; internal set; }

    /// <summary>
    /// PropertyUnderlyingType
    /// </summary>
    public Type? PropertyUnderlyingType { get; private set; }

    /// <summary>
    /// PropertyUnderlyingConverter
    /// </summary>
    public TypeConverter? PropertyUnderlyingConverter { get; private set; }

    /// <summary>
    /// 是否使用最后一个非空值,通常处理合并单元格中的空白错误
    /// </summary>
    internal bool? UseLastNonBlankValue { get; set; }

    /// <summary>
    /// 是否忽略该属性
    /// </summary>
    internal bool? Ignored { get; set; }

    /// <summary>
    /// 是否忽略此列的所有错误
    /// </summary>
    public bool? IgnoreErrors { get; set; }

    /// <summary>
    /// 自定义格式（https://support.office.com/en-us/article/Create-or-delete-a-custom-number-format-78f2a361-936b-4c03-8772-09fab54be7f4）
    /// </summary>
    public string? CustomFormat { get; set; }

    /// <summary>
    /// 从文件导入数据时，请尝试获取给定列的单元格值
    /// </summary>
    internal Func<ExcelColumn, object?, bool>? TryTake { get; set; }

    /// <summary>
    /// 将对象导出到文件时，请尝试将给定列的值设置为单元格
    /// </summary>
    internal Func<ExcelColumn, object?, bool>? TryPut { get; set; }

    public Type? PropertyType { get; private set; }

    public object? DefaultValue { get; set; }

    public DefaultValueAttribute? DefaultValueAttribute { get; private set; }

    internal string? PropertyFullPath { get; private set; }

    private Delegate? Getter { get; set; }

    private Delegate? Setter { get; set; }
    #endregion

    #region Constructors
    public ExcelColumnAttribute()
    {
    }

    public ExcelColumnAttribute(ushort index)
    {
        Index = index;
    }

    public ExcelColumnAttribute(string name)
    {
        Name = name;
    }

    public ExcelColumnAttribute(string name, ushort index)
    {
        Name = name;
        Index = index;
    }
    #endregion

    #region Methods
    public ExcelColumnAttribute Clone()
    {
        return (ExcelColumnAttribute)MemberwiseClone();
    }

    public ExcelColumnAttribute Clone(int index)
    {
        var clone = Clone();
        clone.Index = index;
        return clone;
    }

    public Func<T, object?>? GetGetterOrDefault<T>(T host)
    {
        if (PropertyFullPath.IsNull())
        {
            return null;
        }

        Getter ??= PropertyFullPath.CreateConditionalGetter<T, object?>(host, true);
        return (Func<T, object?>)Getter;
    }

    public Action<T, object?>? GetSetterOrDefault<T>(T host)
    {
        if (PropertyFullPath.IsNull())
        {
            return null;
        }

        Setter ??= PropertyFullPath.CreateConditionalSetter<T, object?>(host, true);
        return (Action<T, object?>)Setter;
    }

    /// <summary>
    /// 合并
    /// </summary>
    /// <param name="source">源属性</param>
    /// <param name="overwrite">如果源的属性为空，则是否覆盖源中的指定属性指定。注释索引和名称一起被视为一个键属性。</param>
    public void MergeFrom(ExcelColumnAttribute source, bool overwrite = true)
    {
        if (source == null)
        {
            return;
        }

        if (source.Index >= 0 || source.Name != null)
        {
            if (overwrite || Index < 0 && Name == null)
            {
                Index = source.Index;
                Name = source.Name;
            }
        }

        if (source.UseLastNonBlankValue != null && (overwrite || UseLastNonBlankValue == null))
        {
            UseLastNonBlankValue = source.UseLastNonBlankValue;
        }

        if (source.Ignored != null && (overwrite || Ignored == null))
        {
            Ignored = source.Ignored;
        }

        if (source.CustomFormat != null && (overwrite || CustomFormat == null))
        {
            CustomFormat = source.CustomFormat;
        }

        if (source.IgnoreErrors != null && (overwrite || IgnoreErrors == null))
        {
            IgnoreErrors = source.IgnoreErrors;
        }

        if (overwrite || TryPut == null)
        {
            TryPut = source.TryPut;
        }

        if (overwrite || TryTake == null)
        {
            TryTake = source.TryTake;
        }

        if (source.PropertyType != null && (overwrite || PropertyType == null))
        {
            SetProperty(source.PropertyType, source.PropertyName, source.PropertyFullPath, source.DefaultValueAttribute);
        }

        if (source.DefaultValue != null && (overwrite || DefaultValue == null))
        {
            DefaultValue = source.DefaultValue;
        }
    }

    /// <summary>
    /// 合并
    /// </summary>
    /// <param name="attributes">需要合并到对象</param>
    /// <param name="overwrite">如果对象的属性为空，是否将指定的属性覆盖到现有对象指定。注释索引和名称一起被视为一个键属性。</param>
    public void MergeTo(Dictionary<string, ExcelColumnAttribute> attributes, bool overwrite = true)
    {
        if (PropertyFullPath is null)
        {
            return;
        }

        var existed = attributes.TryGetValue(PropertyFullPath, out var attribute) ? attribute : null;
        var isIndexSet = Index >= 0;

        if (isIndexSet && !overwrite)
        {
            if (attributes.Any(p => p.Key != PropertyFullPath && p.Value.Index == Index))
            {
                // Clear Index if there is same index already set (with overwrite = false).
                Index = -1;
                isIndexSet = false;
            }
        }

        if (existed != null)
        {
            isIndexSet = isIndexSet && (existed.Index != Index || overwrite);
            existed.MergeFrom(this, overwrite);
            isIndexSet = isIndexSet && existed.Index == Index;
        }
        else
        {
            attributes[PropertyFullPath] = this;
        }

        if (isIndexSet) // True if the index set successfully, otherwise it's been ignored/ cleared.
        {
            // Clear other attributes' Index if they have same index.
            attributes.Where(p => p.Key != PropertyFullPath && p.Value.Index == Index).ForEach(p => p.Value.Index = -1);
        }
    }

    /// <summary>
    /// Set property type, name, also set underlying type and type convert.
    /// </summary>
    public ExcelColumnAttribute SetProperty(PropertyInfo? value, string hostTypeName, string propertyPath)
    {
        if (value is not null && hostTypeName.IsNull())
        {
            throw new ArgumentNullException(nameof(hostTypeName));
        }

        if (value is not null && propertyPath.IsNull())
        {
            throw new ArgumentNullException(nameof(propertyPath));
        }

        SetProperty(value, hostTypeName + "." + propertyPath);
        return this;
    }

    /// <summary>
    /// Set property type, name, also set underlying type and type convert.
    /// </summary>
    public ExcelColumnAttribute SetProperty(PropertyInfo? value, string? propertyFullPath)
    {
        PropertyFullPath = propertyFullPath;

        if (value is null)
        {
            return this;
        }

        ArgumentNullException.ThrowIfNullOrWhiteSpace(propertyFullPath);

        var defaultValueAttribute = value.GetCustomAttribute<DefaultValueAttribute>(true);
        SetProperty(value.PropertyType, value.Name, propertyFullPath, defaultValueAttribute);

        return this;
    }

    private void SetProperty(Type? propertyType, string? propertyName, string? propertyFullPath, DefaultValueAttribute? defaultValueAttribute)
    {
        PropertyFullPath = propertyFullPath;
        PropertyName = propertyName;

        PropertyType = propertyType;
        PropertyUnderlyingType = propertyType is null ? null : Nullable.GetUnderlyingType(propertyType);
        PropertyUnderlyingConverter = PropertyUnderlyingType != null ? TypeDescriptor.GetConverter(PropertyUnderlyingType) : null;
        DefaultValueAttribute = defaultValueAttribute;
    }
    #endregion
}

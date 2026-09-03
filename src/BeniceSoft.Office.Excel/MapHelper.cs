using BeniceSoft.Core;
using NPOI.SS.UserModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace BeniceSoft.Office.Excel;

public partial class MapHelper
{
    private static readonly char[] _columnChars = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z'];
    private static readonly char[] _defaultIgnoredChars = ['`', '~', '!', '@', '#', '$', '%', '^', '&', '*', '-', '_', '+', '=', '|', ',', '.', '/', '?'];
    private static readonly char[] _defaultTruncateChars = ['[', '<', '(', '{'];

    private static readonly DataFormatter _dataFormatter = new();
    private static readonly Type _stringType = typeof(string);
    private static readonly Type _dateTimeType = typeof(DateTime);
    private static readonly Type _objectType = typeof(object);

    private readonly Dictionary<short, ICellStyle> _builtinStyles = [];
    private readonly Dictionary<string, ICellStyle> _customStyles = [];

    public const BindingFlags BindingFlag = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;

    public int MaxLookupRowNum { get; set; } = 20;

    public static void LoadAttributes(Dictionary<string, ExcelColumnAttribute> attributes, Type type)
    {
        if (type == null)
        {
            return;
        }

        foreach (var pi in type.GetProperties(BindingFlag))
        {
            var columnMeta = pi.GetCustomAttribute<ExcelColumnAttribute>(false);
            var ignore = pi.IsDefined<ExcelIgnoreAttribute>();
            var useLastNonBlank = pi.IsDefined<ExcelLastNonBlankAttribute>();

            if (columnMeta == null && !ignore && !useLastNonBlank)
            {
                continue;
            }

            columnMeta ??= new()
            {
                Ignored = ignore ? new bool?(true) : null,
                UseLastNonBlankValue = useLastNonBlank ? new bool?(true) : null
            };

            columnMeta.SetProperty(pi, type.Name, pi.Name);

            // Note that attribute from Map method takes precedence over Attribute meta data.
            columnMeta.MergeTo(attributes, false);
        }
    }

    public static void LoadDynamicAttributes(Dictionary<string, ExcelColumnAttribute> attributes, Dictionary<string, ExcelColumnAttribute> dynamicAttributes, Type dynamicType)
    {
        foreach (var pair in dynamicAttributes)
        {
            var pi = dynamicType.GetProperty(pair.Key);

            if (pi != null)
            {
                pair.Value.SetProperty(pi, dynamicType.Name, pi.Name);
                pair.Value.MergeTo(attributes);
            }
        }
    }

    public void ClearCache()
    {
        _builtinStyles.Clear();
        _customStyles.Clear();
    }

    public void LoadDataFormats(ISheet sheet, int firstDataRowIndex, IEnumerable<ExcelColumn> columns, Dictionary<Type, string> defaultFormats)
    {
        if (sheet == null)
        {
            return;
        }

        if (columns == null)
        {
            return;
        }

        foreach (var column in columns)
        {
            var type = column.Attribute.PropertyType;

            if (column.Attribute.CustomFormat == null)
            {
                if (type != null && !defaultFormats.ContainsKey(type))
                {
                    type = column.Attribute.PropertyUnderlyingType;
                }

                if (type != null && defaultFormats.TryGetValue(type, out var format))
                {
                    column.Attribute.CustomFormat = format;
                }
            }

            var rowIndex = firstDataRowIndex >= 0 ? firstDataRowIndex : sheet.FirstRowNum + 1;
            while (rowIndex <= sheet.LastRowNum && rowIndex <= MaxLookupRowNum)
            {
                var dataRow = sheet.GetRow(rowIndex);
                var cell = dataRow?.GetCell(column.Attribute.Index);

                rowIndex++;
                if (cell?.CellStyle == null)
                {
                    continue;
                }

                column.DataFormat = cell.CellStyle.DataFormat;
                break;
            }
        }
    }

    public ICellStyle? GetCellStyle(ICell? cell, string? customFormat, short? columnFormat)
    {
        ICellStyle? style = null;
        var workbook = cell?.Row.Sheet.Workbook;

        if (customFormat.IsNotNull())
        {
            if (_customStyles.TryGetValue(customFormat, out var value))
            {
                style = value;
            }
            else if (workbook != null)
            {
                style = CreateCellStyle(workbook, customFormat);
                _customStyles[customFormat] = style;
            }
        }
        else if (workbook != null)
        {
            var format = columnFormat ?? 0; // Defaults to 0.

            if (format == 0)
            {
                return null;
            }

            if (_builtinStyles.TryGetValue(format, out var value))
            {
                style = value;
            }
            else
            {
                style = CreateCellStyle(workbook, format)!;
                _builtinStyles[format] = style;
            }
        }

        return style;
    }

    public static ICellStyle CreateCellStyle(IWorkbook workbook, string format)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        if (format.IsNull())
        {
            throw new ArgumentException($"Parameter '{nameof(format)}' cannot be null or white string.");
        }

        var style = workbook.CreateCellStyle();
        style.DataFormat = workbook.CreateDataFormat().GetFormat(format);

        return style;
    }

    public static ICellStyle? CreateCellStyle(IWorkbook workbook, short format)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        if (format == 0)
        {
            return null;
        }

        var style = workbook.CreateCellStyle();
        style.DataFormat = format;

        return style;
    }

    public ICellStyle? GetDefaultStyle(IWorkbook? workbook, object? value, Dictionary<Type, string>? defaultFormats)
    {
        if (value == null || workbook == null || defaultFormats == null)
        {
            return null;
        }

        ICellStyle style;
        var type = value.GetType();

        if (!defaultFormats.TryGetValue(type, out var format))
        {
            return null;
        }

        if (format.IsNull())
        {
            return null;
        }

        if (!_customStyles.TryGetValue(format, out var s))
        {
            style = CreateCellStyle(workbook, format);
            _customStyles[format] = style;
        }
        else
        {
            style = s;
        }

        return style;
    }

    public static CellType GetCellType(ICell cell)
    {
        return cell.CellType == CellType.Formula ? cell.CachedFormulaResultType : cell.CellType;
    }

    public static bool TryGetCellValue(ICell? cell, Type? targetType, TrimSpacesType trimSpacesType, IFormulaEvaluator? evaluator, out object? value)
    {
        value = null;
        if (cell == null)
        {
            return true;
        }

        var cellType = GetCellType(cell);

        if (targetType == _stringType && cellType != CellType.Blank)
        {
            var trimmedValue = TrimString(_dataFormatter.FormatCellValue(cell, evaluator));
            value = trimmedValue?.Length == 0 ? null : trimmedValue;

            return true;
        }

        var success = true;

        string? TrimString(string? raw)
        {
            return trimSpacesType switch
            {
                TrimSpacesType.None => raw,
                TrimSpacesType.Start => raw?.TrimStart(),
                TrimSpacesType.End => raw?.TrimEnd(),
                TrimSpacesType.Both => raw?.Trim(),
                _ => null,
            };
        }

        switch (cellType)
        {
            case CellType.String:
                value = TrimString(cell.StringCellValue);
                break;

            case CellType.Numeric:

                if (DateUtil.IsCellDateFormatted(cell) || targetType == _dateTimeType || targetType == typeof(DateTimeOffset))
                {
                    value = cell.DateCellValue;
                }
                else // Number type
                {
                    value = cell.NumericCellValue;
                }

                break;

            case CellType.Boolean:

                value = cell.BooleanCellValue;
                break;

            case CellType.Error:
            case CellType.Unknown:
            case CellType.Blank:
                // Dose nothing to keep return value null.
                break;

            default:
                success = false;
                break;
        }

        return success;
    }

    public static (PropertyInfo? propertyInfo, string fullPath) GetPropertyInfo<T>(Expression<Func<T, object?>> propertySelector)
    {
        if (propertySelector is not LambdaExpression lambdaExpression)
        {
            throw new ArgumentException($"Unsupported property selector: {propertySelector}", nameof(propertySelector));
        }

        var pathBuilder = new StringBuilder();
        var body = lambdaExpression.Body;

        while (body is MemberExpression or UnaryExpression { Operand: MemberExpression })
        {
            var memberAccess = body as MemberExpression ?? (MemberExpression)((UnaryExpression)body).Operand;

            if (pathBuilder.Length > 0)
            {
                pathBuilder.Insert(0, ".");
            }

            pathBuilder.Insert(0, memberAccess.Member.Name);
            body = memberAccess.Expression;
        }

        if (pathBuilder.Length > 0)
        {
            pathBuilder.Insert(0, typeof(T).Name + ".");
        }

        var lambdaBody = lambdaExpression.Body.NodeType == ExpressionType.MemberAccess ? (MemberExpression)lambdaExpression.Body : (MemberExpression)((UnaryExpression)lambdaExpression.Body).Operand; // for nullable value such as int?

        var propertyInfo = lambdaBody.Expression?.Type.GetProperty(lambdaBody.Member.Name);
        return (propertyInfo, pathBuilder.ToString());
    }

    public static (PropertyInfo? propertyInfo, string fullPath) GetPropertyInfo<T>(string propertyPath)
    {
        if (propertyPath.IsNull())
        {
            throw new ArgumentException("propertyPath is null or white space", nameof(propertyPath));
        }

        var type = typeof(T);
        var fullPath = type.Name + "." + propertyPath;

        if (type == typeof(object))
        {
            return (null, fullPath);
        }

        var propertyName = string.Empty;
        var param = Expression.Parameter(type, "x");
        Expression body = param;

        foreach (var member in propertyPath.Split('.'))
        {
            try
            {
                type = body.Type;
                var memberExpression = Expression.PropertyOrField(body, member);
                propertyName = member;
                body = memberExpression;
            }
            catch
            {
                return (null, fullPath);
            }
        }

        var pi = type.GetProperty(propertyName);
        return (pi, fullPath);
    }

    public static PropertyInfo? GetPropertyInfoByExpression<T>(Expression<Func<T, object?>> propertySelector)
    {
        if (propertySelector is not LambdaExpression expression)
        {
            throw new ArgumentException("Only LambdaExpression is allowed!", nameof(propertySelector));
        }

        var body = expression.Body.NodeType == ExpressionType.MemberAccess ? (MemberExpression)expression.Body : (MemberExpression)((UnaryExpression)expression.Body).Operand;

        // body.Member will return the MemberInfo of base class, so we have to get it from T...
        //return (PropertyInfo)body.Member;
        return typeof(T).GetMember(body.Member.Name)[0] as PropertyInfo;
    }

    public static string? GetRefinedName(string? name, char[]? ignoringChars, char[]? truncatingChars)
    {
        if (name == null)
        {
            return null;
        }

        name = MapRegex().Replace(name, string.Empty);
        var ignoredChars = ignoringChars ?? _defaultIgnoredChars;
        var truncateChars = truncatingChars ?? _defaultTruncateChars;

        name = ignoredChars.Aggregate(name, (current, c) => current.Replace(c.ToString(), string.Empty));

        var index = name.IndexOfAny(truncateChars);
        if (index >= 0)
        {
            name = name.Remove(index);
        }

        return name;
    }

    public static string GetVariableName(string? rawName, char[]? ignoringChars, char[]? truncatingChars, int columnIndex)
    {
        rawName = GetRefinedName(rawName, ignoringChars, truncatingChars);

        if (rawName.IsNull())
        {
            rawName = GetExcelColumnName(columnIndex);
        }

        return rawName;
    }

    public static string GetExcelColumnName(int columnIndex)
    {
        if (columnIndex is < 0 or > 16383)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex));
        }

        var columnName = string.Empty;
        var result = columnIndex;
        do
        {
            var reminder = result % _columnChars.Length;
            columnName = _columnChars[reminder] + columnName;
            result = result / _columnChars.Length - 1;
        }
        while (result != -1);

        return columnName;
    }

    public Type? InferColumnDataType(ISheet sheet, int headerRowIndex, int columnIndex)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);

        Type? type = null;
        var rowIndex = headerRowIndex >= 0 ? headerRowIndex + 1 : 0;
        var typeDetected = false;

        while (!typeDetected && rowIndex <= sheet.LastRowNum && rowIndex <= MaxLookupRowNum)
        {
            var row = sheet.GetRow(rowIndex);

            var cell = row?.GetCell(columnIndex);
            if (cell != null)
            {
                var cellType = GetCellType(cell);
                typeDetected = true;
                switch (cellType)
                {
                    case CellType.Boolean:
                        type = typeof(bool);
                        break;
                    case CellType.Numeric:
                        type = DateUtil.IsCellDateFormatted(cell) ? _dateTimeType : typeof(double);
                        break;
                    case CellType.String:
                        type = _stringType;
                        break;
                    default:
                        typeDetected = false;
                        break;
                }
            }

            rowIndex++;
        }

        return type;
    }

    internal static void EnsureDefaultFormats(IEnumerable<ExcelColumn> columns, Dictionary<Type, string> defaultFormats)
    {
        if (!defaultFormats.ContainsKey(_dateTimeType))
        {
            defaultFormats[_dateTimeType] = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
        }

        foreach (var column in columns)
        {
            var attribute = column.Attribute;
            if (column.DataFormat == null && attribute.PropertyFullPath != null && attribute.CustomFormat == null)
            {
                var type = attribute.PropertyType;
                if (type is null)
                {
                    continue;
                }

                if (defaultFormats.TryGetValue(type, out var format))
                {
                    attribute.CustomFormat = format;
                }
            }
        }
    }

    internal static bool TryConvertType(object? value, ExcelColumn? column, bool useDefaultValueAttr, out object? result)
    {
        result = null;
        if (column?.Attribute.PropertyType == null)
        {
            return false;
        }

        var targetType = column.Attribute.PropertyType;
        var underlyingType = column.Attribute.PropertyUnderlyingType;
        targetType = underlyingType ?? targetType;

        if (value == null)
        {
            if (column.Attribute.DefaultValue != null)
            {
                result = column.Attribute.DefaultValue;
            }
            else if (useDefaultValueAttr && column.Attribute.DefaultValueAttribute != null)
            {
                result = column.Attribute.DefaultValueAttribute.Value;
            }

            if (result is not null && result.GetType() != targetType)
            {
                result = Convert.ChangeType(result, targetType);
            }

            return true;
        }

        if (targetType == _dateTimeType || targetType == typeof(DateTimeOffset))
        {
            return TryConvertToDateTime(value, targetType == typeof(DateTimeOffset), column.Attribute.CustomFormat, ref result);
        }

        if (value is string stringValue)
        {
            if (targetType == _stringType)
            {
                result = stringValue;
                return true;
            }

            if (targetType.IsNumeric() && double.TryParse(stringValue, NumberStyles.Any, null, out var doubleResult))
            {
                result = Convert.ChangeType(doubleResult, targetType);
                return true;
            }

            if (targetType.IsEnum)
            {
                result = Enum.Parse(targetType, stringValue, true);
                return true;
            }

            if (targetType == typeof(Guid))
            {
                var parsed = Guid.TryParse(stringValue, out var guidResult);
                result = guidResult;
                return parsed;
            }

            // Ensure we are not throwing exception and just read a null for nullable property.
            if (underlyingType != null)
            {
                if (stringValue.IsNull())
                {
                    return true;
                }

                var converter = column.Attribute.PropertyUnderlyingConverter;
                if (converter is null || !converter.IsValid(value))
                {
                    return false;
                }
            }
        }

        try
        {
            result = Convert.ChangeType(value, targetType);
        }
        catch
        {
            return false;
        }

        return true;
    }

    private static bool TryConvertToDateTime(object value, bool isDateTimeOffset, string? format, ref object? result)
    {
        if (value is string stringValue)
        {
            // string to DateTimeOffset
            if (isDateTimeOffset)
            {
                if (format.IsNotNull())
                {
                    if (DateTimeOffset.TryParseExact(stringValue, format,
                            CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var dateTimeOffset2))
                    {
                        result = dateTimeOffset2;
                        return true;
                    }
                }

                if (DateTimeOffset.TryParse(stringValue,
                        CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var dateTimeOffset))
                {
                    result = dateTimeOffset;
                    return true;
                }

                return false;
            }

            // string to DateTime
            if (format.IsNotNull())
            {
                if (DateTime.TryParseExact(stringValue, format, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var dateTime2))
                {
                    result = dateTime2;
                    return true;
                }
            }

            if (DateTime.TryParse(stringValue, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var dateTime))
            {
                result = dateTime;
                return true;
            }

            return false;
        }

        if (value is DateTime dateTimeValue)
        {
            // Ternary expression will implicitly convert result as a DateTimeOffset.
            if (isDateTimeOffset)
            {
                result = new DateTimeOffset(dateTimeValue);
            }
            else
            {
                result = dateTimeValue;
            }

            return true;
        }

        return false;
    }

    internal static Type GetConcreteType<T>(T?[] objects)
    {
        var type = typeof(T);
        if (type != _objectType)
        {
            return type;
        }

        foreach (var o in objects)
        {
            if (o == null)
            {
                continue;
            }

            type = o.GetType();
            if (type != _objectType)
            {
                break;
            }
        }

        return type;
    }

    [GeneratedRegex(@"\s")]
    private static partial Regex MapRegex();
}

public enum TrimSpacesType
{
    None,
    Start,
    End,
    Both
}
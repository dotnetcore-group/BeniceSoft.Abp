using BeniceSoft.Core;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Office.Excel;

/// <summary>
/// doc to https://github.com/donnytian/Npoi.Mapper
/// </summary>
public class ExcelMapper
{
    #region Members
    private readonly MapHelper _helper = new();
    private Func<ExcelColumn, bool>? _columnFilter;
    private Func<ExcelColumn, object?, bool>? _defaultTakeResolver;
    private Func<ExcelColumn, object?, bool>? _defaultPutResolver;
    private Action<ICell>? _headerAction;
    private IWorkbook? _workbook;

    /// <summary>
    /// 存储类型
    /// </summary>
    internal Dictionary<Type, string> TypeFormats { get; } = [];

    private Dictionary<string, ExcelColumnAttribute> Attributes { get; } = [];

    private Dictionary<string, ExcelColumnAttribute> DynamicAttributes { get; } = [];

    /// <summary>
    /// 跟踪对象
    /// </summary>
    private Dictionary<string, Dictionary<Type, List<object>>> TrackedColumns { get; } = [];

    public IFormulaEvaluator? FormulaEvaluator { get; private set; }

    /// <summary>
    /// Excel工作簿
    /// </summary>
    public IWorkbook Workbook
    {
        get => _workbook ?? throw new InvalidOperationException("Workbook has not been initialized.");
        private set
        {
            if (!ReferenceEquals(value, _workbook))
            {
                TrackedColumns.Clear();
                _helper.ClearCache();

                if (value is HSSFWorkbook)
                {
                    FormulaEvaluator = new HSSFFormulaEvaluator(value);
                }
                else if (value is XSSFWorkbook)
                {
                    FormulaEvaluator = new XSSFFormulaEvaluator(value);
                }
            }

            _workbook = value;
        }
    }

    /// <summary>
    /// 忽略列标题的字符
    /// </summary>
    public char[]? IgnoredNameChars { get; set; }

    /// <summary>
    /// 列名将从此数组中的任何字符中截断
    /// </summary>
    public char[]? TruncateNameFrom { get; set; }

    /// <summary>
    /// 是否将第一行作为列标题,默认值为true
    /// </summary>
    public bool HasHeader { get; set; } = true;

    /// <summary>
    /// 获取或设置第一行的从零开始的索引,如果未设置,将自动检测
    /// </summary>
    public int FirstRowIndex { get; set; } = -1;

    public bool SkipBlankRows { get; set; }

    public TrimSpacesType TrimSpaces { get; set; } = TrimSpacesType.None;

    public bool UseDefaultValueAttribute { get; set; }

    public bool SkipWriteDefaultValue { get; set; }

    public bool SkipHiddenRows { get; set; }
    #endregion

    #region Constructors
    public ExcelMapper()
    {
    }

    public ExcelMapper(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using (stream)
        {
            Workbook = WorkbookFactory.Create(stream);
        }
    }

    public ExcelMapper(IWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        Workbook = workbook;
    }

    public ExcelMapper(string filePath) : this(new FileStream(filePath, FileMode.Open))
    {
    }
    #endregion

    #region Methods
    public ExcelMapper Map(Func<ExcelColumn, bool> columnFilter, Func<ExcelColumn, object?, bool>? tryTake = null, Func<ExcelColumn, object?, bool>? tryPut = null)
    {
        _columnFilter = columnFilter;
        _defaultPutResolver = tryPut;
        _defaultTakeResolver = tryTake;

        return this;
    }

    public ExcelMapper Map(ExcelColumnAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        if (attribute.PropertyType != null)
        {
            attribute.MergeTo(Attributes);
        }
        else if (attribute.PropertyName != null) // For dynamic type
        {
            if (DynamicAttributes.TryGetValue(attribute.PropertyName, out var dynamicAttribute))
            {
                dynamicAttribute.MergeFrom(attribute);
            }
            else
            {
                // Ensures column name for the first time mapping.
                attribute.Name ??= attribute.PropertyName;
                DynamicAttributes[attribute.PropertyName] = attribute;
            }
        }
        else
        {
            throw new InvalidOperationException("Either PropertyName or property selector should be specified for a valid mapping!");
        }

        return this;
    }

    public ExcelMapper UseLastNonBlankValue<T>(Expression<Func<T, object?>> propertySelector)
    {
        var (pi, fullPath) = MapHelper.GetPropertyInfo(propertySelector);
        new ExcelColumnAttribute { UseLastNonBlankValue = true }.SetProperty(pi!, fullPath).MergeTo(Attributes);

        return this;
    }

    public ExcelMapper Ignore<T>(Expression<Func<T, object?>> propertySelector)
    {
        var (pi, fullPath) = MapHelper.GetPropertyInfo(propertySelector);
        new ExcelColumnAttribute { Ignored = true }.SetProperty(pi!, fullPath).MergeTo(Attributes);

        return this;
    }

    public ExcelMapper Format<T>(string customFormat, Expression<Func<T, object?>> propertySelector)
    {
        var (pi, fullPath) = MapHelper.GetPropertyInfo(propertySelector);
        new ExcelColumnAttribute { CustomFormat = customFormat }.SetProperty(pi!, fullPath).MergeTo(Attributes);

        return this;
    }

    public ExcelMapper ForHeader(Action<ICell> headerAction)
    {
        _headerAction = headerAction;
        return this;
    }

    #region Import
    public IEnumerable<ExcelRow<T>> Take<T>(string sheetName, int maxErrorRows = 10, Func<T>? objectInitializer = null)
        where T : class
    {
        var sheet = Workbook.GetSheet(sheetName);
        return Take(sheet, maxErrorRows, objectInitializer);
    }

    public IEnumerable<ExcelRow<T>> Take<T>(int sheetIndex = 0, int maxErrorRows = 10, Func<T>? objectInitializer = null)
        where T : class
    {
        var sheet = Workbook.GetSheetAt(sheetIndex);
        return Take(sheet, maxErrorRows, objectInitializer);
    }

    public IEnumerable<ExcelRow<dynamic>> TakeDynamicWithColumnType(Func<ICell, Type>? columnType, string sheetName, int maxErrorRows = 100)
    {
        var sheet = Workbook.GetSheet(sheetName);
        return Take<object>(sheet, maxErrorRows, null, columnType);
    }

    private IEnumerable<ExcelRow<T>> Take<T>(ISheet sheet, int maxErrorRows, Func<T>? objectInitializer = null, Func<ICell, Type>? columnType = null)
        where T : class
    {
        if (sheet == null || sheet.PhysicalNumberOfRows < 1)
        {
            yield break;
        }

        var firstRowIndex = GetFirstRowIndex(sheet);
        var firstRow = sheet.GetRow(firstRowIndex);

        var targetType = typeof(T);
        if (targetType == typeof(object)) // Dynamic type.
        {
            targetType = GetDynamicType(sheet, columnType);
            MapHelper.LoadDynamicAttributes(Attributes, DynamicAttributes, targetType);
            DynamicAttributes.Clear(); // Avoid mixed with other sheet.
        }

        // Scan object attributes.
        MapHelper.LoadAttributes(Attributes, targetType);

        // Read the first row to get column information.
        var columns = GetColumns(firstRow, targetType);

        // Detect column format based on the first non-null cell.
        _helper.LoadDataFormats(sheet, HasHeader ? firstRowIndex + 1 : firstRowIndex, columns, TypeFormats);

        // Loop rows in file. Generate one target object for each row.
        var errorCount = 0;
        var firstDataRowIndex = HasHeader ? firstRowIndex + 1 : firstRowIndex;
        foreach (IRow row in sheet)
        {
            if (maxErrorRows > 0 && errorCount >= maxErrorRows)
            {
                break;
            }

            if (row.RowNum < firstDataRowIndex)
            {
                continue;
            }

            if (SkipHiddenRows && row.Hidden.HasValue && row.Hidden.Value)
            {
                continue;
            }

            if (SkipBlankRows && row.Cells.All(IsCellBlank))
            {
                continue;
            }

            var obj = objectInitializer == null ? Activator.CreateInstance(targetType) : objectInitializer();
            var rowInfo = new ExcelRow<T>
            {
                RowNumber = row.RowNum,
                Value = obj as T,
                ErrorColumnIndex = -1,
                ErrorMessage = string.Empty
            };

            LoadRowData(columns, row, obj as T, rowInfo);

            if (rowInfo.ErrorColumnIndex >= 0)
            {
                errorCount++;
                //rowInfo.Value = default(T);
            }

            yield return rowInfo;
        }
    }

    private static IEnumerable<T> Import<T>(IEnumerable<ExcelRow<T>> rows)
        where T : class
    {
        if (rows.IsNull())
        {
            yield break;
        }

        foreach (var row in rows)
        {
            yield return row.Value!;
        }
    }

    public IEnumerable<T> Import<T>(int sheetIndex = 0, int maxErrorRows = 10, Func<T>? objectInitializer = null)
        where T : class
    {
        return Import(Take<T>(sheetIndex, maxErrorRows, objectInitializer));
    }

    public IEnumerable<T> Import<T>(string sheetName, int maxErrorRows = 10, Func<T>? objectInitializer = null)
    where T : class
    {
        return Import(Take<T>(sheetName, maxErrorRows, objectInitializer));
    }

    private Type GetDynamicType(ISheet sheet, Func<ICell, Type>? getColumnType)
    {
        var firstRowIndex = GetFirstRowIndex(sheet);
        var firstRow = sheet.GetRow(firstRowIndex);

        var names = new Dictionary<string, Type>();

        foreach (var header in firstRow)
        {
            var column = GetColumnByDynamicAttribute(header);
            var type = getColumnType?.Invoke(header)
                ?? _helper.InferColumnDataType(sheet, HasHeader ? firstRowIndex : -1, header.ColumnIndex);

            if (column != null)
            {
                names[column.Attribute.PropertyName!] = type ?? typeof(string);
            }
            else
            {
                var headerValue = GetHeaderValue(header);
                var tempColumn = new ExcelColumn(headerValue, header.ColumnIndex);
                if (_columnFilter != null && !_columnFilter(tempColumn))
                {
                    continue;
                }

                string propertyName;
                if (HasHeader && MapHelper.GetCellType(header) == CellType.String)
                {
                    propertyName = MapHelper.GetVariableName(header.StringCellValue, IgnoredNameChars,
                        TruncateNameFrom, header.ColumnIndex);
                }
                else
                {
                    propertyName = MapHelper.GetVariableName(null, null, null, header.ColumnIndex);
                }

                names[propertyName] = type ?? typeof(string);
                DynamicAttributes[propertyName] = new ExcelColumnAttribute((ushort)header.ColumnIndex) { PropertyName = propertyName };
            }
        }

        return AnonymousTypeFactory.CreateType(names, true);
    }

    private static bool IsCellBlank(ICell cell)
    {
        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue.IsNull(),
            CellType.Blank => true,
            _ => false,
        };
    }

    private List<ExcelColumn> GetColumns(IRow headerRow, Type type)
    {
        var sheetName = headerRow.Sheet.SheetName;
        var columns = new List<ExcelColumn>();
        var columnsCache = new List<object>(); // Cached for export usage.

        // Prepare a list of ColumnInfo by the first row.
        foreach (var header in headerRow)
        {
            // Custom mappings via attributes.
            var column = GetColumnByAttribute(header, type);

            // Naming convention.
            if (column == null && HasHeader && MapHelper.GetCellType(header) == CellType.String)
            {
                var s = header.StringCellValue;

                if (s.IsNotNull())
                {
                    column = GetColumnByName(s.Trim(), header.ColumnIndex, type);
                }
            }

            // Column filter.
            if (column == null)
            {
                column = GetColumnByFilter(header, _columnFilter);

                if (column != null) // Set default resolvers since the column is not mapped explicitly.
                {
                    column.Attribute.TryPut = _defaultPutResolver;
                    column.Attribute.TryTake = _defaultTakeResolver;
                }
            }

            if (column == null)
            {
                continue; // No property was mapped to this column.
            }

            if (header.CellStyle != null)
            {
                column.HeaderFormat = header.CellStyle.DataFormat;
            }

            columns.Add(column);
            columnsCache.Add(column);
        }

        var typeDict = TrackedColumns.TryGetValue(sheetName, out var trackedColumn) ? trackedColumn : TrackedColumns[sheetName] = [];

        typeDict[type] = columnsCache;

        return columns;
    }

    private ExcelColumn? GetColumnByDynamicAttribute(ICell header)
    {
        var cellType = MapHelper.GetCellType(header);
        var index = header.ColumnIndex;

        foreach (var pair in DynamicAttributes)
        {
            var attribute = pair.Value;

            // If no header, cannot get a ColumnInfo by resolving header string.
            if (!HasHeader && attribute.Index < 0)
            {
                continue;
            }

            var headerValue = HasHeader ? GetHeaderValue(header) : null;
            var indexMatch = attribute.Index == index;
            var nameMatch = cellType == CellType.String && string.Equals(attribute.Name, header.StringCellValue, StringComparison.Ordinal);

            // Index takes precedence over Name.
            if (indexMatch || attribute.Index < 0 && nameMatch)
            {
                // Use a clone so no pollution to original attribute,
                // The origin might be used later again for multi-column/DefaultResolverType purpose.
                attribute = attribute.Clone(index);
                return new ExcelColumn(headerValue, attribute);
            }
        }

        return null;
    }

    private ExcelColumn? GetColumnByAttribute(ICell header, Type type)
    {
        var cellType = MapHelper.GetCellType(header);
        var index = header.ColumnIndex;

        foreach (var pair in Attributes)
        {
            var attribute = pair.Value;

            if (!pair.Key.StartsWith(type.Name + '.') || attribute.Ignored == true)
            {
                continue;
            }

            // If no header, cannot get a ColumnInfo by resolving header string.
            if (!HasHeader && attribute.Index < 0)
            {
                continue;
            }

            var headerValue = HasHeader ? GetHeaderValue(header) : null;
            var indexMatch = attribute.Index == index;
            var nameMatch = cellType == CellType.String && string.Equals(attribute.Name?.Trim(), header.StringCellValue?.Trim(), StringComparison.Ordinal);

            // Index takes precedence over Name.
            if (indexMatch || attribute.Index < 0 && nameMatch)
            {
                // Use a clone so no pollution to original attribute,
                // The origin might be used later again for multi-column/DefaultResolverType purpose.
                attribute = attribute.Clone(index);
                return new ExcelColumn(headerValue, attribute);
            }
        }

        return null;
    }

    private ExcelColumn? GetColumnByName(string name, int index, Type type)
    {
        // First attempt: search by string (ignore case).
        var pi = type.GetProperty(name, MapHelper.BindingFlag);

        if (pi == null)
        {
            // Second attempt: search display name of DisplayAttribute if any.
            foreach (var propertyInfo in type.GetProperties(MapHelper.BindingFlag))
            {
                var attributes = propertyInfo.GetCustomAttributes<DisplayAttribute>(false);

                if (attributes.Any(att => att.Name is not null && att.Name.EqualsTo(name, StringComparison.CurrentCultureIgnoreCase)))
                {
                    pi = propertyInfo;
                    break;
                }
            }
        }

        if (pi == null)
        {
            // Third attempt: remove ignored chars and do the truncation.
            pi = type.GetProperty(MapHelper.GetRefinedName(name, IgnoredNameChars, TruncateNameFrom)!, MapHelper.BindingFlag);
        }

        if (pi == null)
        {
            return null;
        }

        ExcelColumnAttribute? attribute = null;
        var key = type.Name + "." + pi.Name;

        if (Attributes.TryGetValue(key, out var attr))
        {
            if (attr.Ignored == true)
            {
                return null;
            }

            attribute = attr.Clone(index);
        }

        return attribute == null ? new ExcelColumn(name, index, pi, type.Name, pi.Name) : new ExcelColumn(name, attribute);
    }

    private static ExcelColumn? GetColumnByFilter(ICell header, Func<ExcelColumn, bool>? columnFilter)
    {
        if (columnFilter == null)
        {
            return null;
        }

        var headerValue = GetHeaderValue(header);
        var column = new ExcelColumn(headerValue, header.ColumnIndex);

        return columnFilter(column) ? column : null;
    }

    private void LoadRowData<T>(IEnumerable<ExcelColumn> columns, IRow row, T? target, ExcelRow<T> rowInfo)
    {
        var errorIndex = -1;
        string? errorMessage = null;

        void ColumnFailed(ExcelColumn column, string message)
        {
            if (errorIndex >= 0)
            {
                return; // Ensures the first error will not be overwritten.
            }

            if (column.Attribute.IgnoreErrors == true)
            {
                return;
            }

            errorIndex = column.Attribute.Index;
            errorMessage = message;
        }

        foreach (var column in columns)
        {
            var index = column.Attribute.Index;
            if (index < 0)
            {
                continue;
            }

            column.RowTag = rowInfo.RowTag;
            try
            {
                var cell = row.GetCell(index);
                var propertyType = column.Attribute.PropertyUnderlyingType ?? column.Attribute.PropertyType;

                if (!MapHelper.TryGetCellValue(cell, propertyType, TrimSpaces, FormulaEvaluator, out var valueObj))
                {
                    ColumnFailed(column, "CellType is not supported yet!");
                    continue;
                }

                valueObj = column.RefreshAndGetValue(valueObj);

                if (column.Attribute.TryTake != null)
                {
                    if (!column.Attribute.TryTake(column, target))
                    {
                        ColumnFailed(column, "Returned failure by custom cell resolver!");
                    }
                }
                else if (propertyType != null)
                {
                    // Change types between IConvertible objects, such as double, float, int and etc.
                    if (MapHelper.TryConvertType(valueObj, column, UseDefaultValueAttribute, out var result))
                    {
                        column.Attribute.GetSetterOrDefault(target)?.Invoke(target, result);
                    }
                    else
                    {
                        ColumnFailed(column, "Cannot convert value to the property type!");
                    }
                }
            }
            catch (Exception e)
            {
                ColumnFailed(column, e.ToString());
            }
            finally
            {
                rowInfo.RowTag = column.RowTag;
                column.RowTag = null;
            }
        }

        rowInfo.ErrorColumnIndex = errorIndex;
        rowInfo.ErrorMessage = errorMessage;
    }

    private static object? GetHeaderValue(ICell header)
    {
        var cellType = header.CellType;

        if (cellType == CellType.Formula)
        {
            cellType = header.CachedFormulaResultType;
        }

        var value = cellType switch
        {
            CellType.Numeric => (object)header.NumericCellValue,
            CellType.String => header.StringCellValue,
            _ => null,
        };
        return value;
    }

    private void LoadWorkbookFromFile(string path)
    {
        Workbook = WorkbookFactory.Create(new FileStream(path, FileMode.Open));
    }
    #endregion

    #region Export
    private void Put<T>(ISheet sheet, IEnumerable<T> objects, bool overwrite)
    {
        var sheetName = sheet.SheetName;
        var firstRowIndex = GetFirstRowIndex(sheet);
        var firstRow = sheet.GetRow(firstRowIndex);
        var objectArray = objects as T[] ?? objects.ToArray();
        var type = MapHelper.GetConcreteType(objectArray);

        var columns = GetTrackedColumns(sheetName, type) ?? GetColumns(firstRow ?? PopulateFirstRow(sheet, null, type), type);
        firstRow = sheet.GetRow(firstRowIndex) ?? PopulateFirstRow(sheet, columns, type);

        var rowIndex = overwrite ? HasHeader ? firstRowIndex + 1 : firstRowIndex : sheet.GetRow(sheet.LastRowNum) != null ? sheet.LastRowNum + 1 : sheet.LastRowNum;

        MapHelper.EnsureDefaultFormats(columns, TypeFormats);

        foreach (var o in objectArray)
        {
            var row = sheet.GetRow(rowIndex);

            if (overwrite && row != null)
            {
                row.Cells?.ForEach(c => c.SetCellType(CellType.Blank)); // erase content and try keep format.
            }

            row ??= sheet.CreateRow(rowIndex);

            foreach (var column in columns)
            {
                var value = column.Attribute.GetGetterOrDefault(o)?.Invoke(o);
                var cell = row.GetCell(column.Attribute.Index, MissingCellPolicy.CREATE_NULL_AS_BLANK);

                column.CurrentValue = value;
                if (column.Attribute.TryPut == null || column.Attribute.TryPut(column, o))
                {
                    SetCell(cell, column.CurrentValue, column, setStyle: overwrite);
                }
            }

            rowIndex++;
        }

        // Remove not used rows if any.
        while (overwrite && rowIndex <= sheet.LastRowNum)
        {
            var row = sheet.GetRow(rowIndex);
            if (row != null)
            {
                sheet.RemoveRow(row);
            }

            rowIndex++;
        }

        // Injects custom action for headers.
        if (overwrite && HasHeader && _headerAction != null)
        {
            firstRow?.Cells.ForEach(c => _headerAction(c));
        }
    }

    private void Save<T>(Stream stream, ISheet sheet, IEnumerable<T> objects, bool leaveOpen = false, bool overwrite = true)
    {
        Put(sheet, objects, overwrite);
        Workbook.Write(stream, leaveOpen);
    }

    private IRow PopulateFirstRow(ISheet sheet, List<ExcelColumn>? columns, Type type)
    {
        var row = sheet.CreateRow(GetFirstRowIndex(sheet));

        // Use existing column populate the first row.

        if (columns != null)
        {
            foreach (var column in columns)
            {
                var cell = row.CreateCell(column.Attribute.Index);

                if (!HasHeader)
                {
                    continue;
                }

                SetCell(cell, column.Attribute.Name ?? column.HeaderValue, column, true);
            }

            return row;
        }

        // If no column cached, populate the first row with attributes and object properties.

        MapHelper.LoadAttributes(Attributes, type);

        var attributes = Attributes.Where(p => p.Value.PropertyFullPath?.StartsWith(type.Name + ".") == true);
        var properties = type.GetProperties(MapHelper.BindingFlag).FindAll(p => p.PropertyType.CanBeExported()).ToList();

        // Firstly populate for those have Attribute specified.
        foreach (var attr in attributes)
        {
            var attribute = attr.Value;
            if (attr.Value.Index < 0)
            {
                continue;
            }

            var cell = row.CreateCell(attribute.Index);
            if (HasHeader)
            {
                cell.SetCellValue(attribute.Name ?? attribute.PropertyName);
            }

            properties.RemoveAll(p => p.Name == attribute.PropertyName); // Remove populated property.
        }

        var index = 0;

        // Then populate for those do not have Attribute specified.
        foreach (var pi in properties)
        {
            var key = type.Name + "." + pi.Name;
            var attribute = Attributes.TryGetValue(key, out var attribute1) ? attribute1 : null;
            if (attribute?.Ignored == true)
            {
                continue;
            }

            while (row.GetCell(index) != null)
            {
                index++;
            }

            var cell = row.CreateCell(index);
            if (HasHeader)
            {
                cell.SetCellValue(attribute?.Name ?? pi.Name);
            }
            else
            {
                new ExcelColumnAttribute { Index = index }.SetProperty(pi, type.Name, pi.Name).MergeTo(Attributes);
            }

            index++;
        }

        return row;
    }

    private int GetFirstRowIndex(ISheet sheet)
    {
        return FirstRowIndex >= 0 ? FirstRowIndex : sheet.FirstRowNum;
    }

    private List<ExcelColumn>? GetTrackedColumns(string sheetName, Type type)
    {
        if (!TrackedColumns.ContainsKey(sheetName))
        {
            return null;
        }

        IEnumerable<ExcelColumn>? columns = null;

        var cols = TrackedColumns[sheetName];
        if (cols.TryGetValue(type, out var col))
        {
            columns = col.OfType<ExcelColumn>();
        }

        return columns?.ToList();
    }

    private void SetCell(ICell cell, object? value, ExcelColumn? column, bool isHeader = false, bool setStyle = true)
    {
        if (value is null or ICollection)
        {
            cell.SetCellValue((string?)null);
        }
        else if (column != null && SkipWriteDefaultValue && !isHeader && (Equals(column.Attribute.DefaultValue, value) || UseDefaultValueAttribute && Equals(column.Attribute.DefaultValueAttribute?.Value, value)))
        {
            cell.SetCellValue((string?)null);
        }
        else if (value is DateTime time)
        {
            cell.SetCellValue(time);
        }
        else if (value.GetType().IsNumeric())
        {
            cell.SetCellValue(Convert.ToDouble(value));
        }
        else if (value is bool b)
        {
            cell.SetCellValue(b);
        }
        else
        {
            cell.SetCellValue(value.ToString());
        }

        if (column != null && setStyle)
        {
            column.SetCellStyle(cell, value, isHeader, TypeFormats, _helper);
        }
    }

    public void Put<T>(IEnumerable<T> objects, string sheetName, bool overwrite = true)
    {
        if (_workbook == null)
        {
            Workbook = new XSSFWorkbook();
        }

        var sheet = Workbook.GetSheet(sheetName) ?? Workbook.CreateSheet(sheetName);
        Put(sheet, objects, overwrite);
    }

    public void Put<T>(IEnumerable<T> objects, int sheetIndex = 0, bool overwrite = true)
    {
        if (_workbook == null)
        {
            Workbook = new XSSFWorkbook();
        }

        var sheet = Workbook.NumberOfSheets > sheetIndex ? Workbook.GetSheetAt(sheetIndex) : Workbook.CreateSheet();
        Put(sheet, objects, overwrite);
    }

    public void Export<T>(string path, IEnumerable<T> objects, string sheetName, bool leaveOpen = false, bool overwrite = true, bool xlsx = true)
    {
        if (_workbook == null && !overwrite)
        {
            LoadWorkbookFromFile(path);
        }

        using var fs = File.Open(path, FileMode.Create, FileAccess.Write);
        Export(fs, objects, sheetName, leaveOpen, overwrite, xlsx);
    }

    public void Export<T>(string path, IEnumerable<T> objects, int sheetIndex = 0, bool leaveOpen = false, bool overwrite = true, bool xlsx = true)
    {
        if (_workbook == null && !overwrite)
        {
            LoadWorkbookFromFile(path);
        }

        using var fs = File.Open(path, FileMode.Create, FileAccess.Write);
        Export(fs, objects, sheetIndex, leaveOpen, overwrite, xlsx);
    }

    public void Export<T>(Stream stream, IEnumerable<T> objects, string sheetName, bool leaveOpen = false, bool overwrite = true, bool xlsx = true)
    {
        if (_workbook == null)
        {
            Workbook = xlsx ? new XSSFWorkbook() : new HSSFWorkbook();
        }

        var sheet = Workbook.GetSheet(sheetName) ?? Workbook.CreateSheet(sheetName);
        Save(stream, sheet, objects, leaveOpen, overwrite);
    }

    public void Export<T>(Stream stream, IEnumerable<T> objects, int sheetIndex = 0, bool leaveOpen = false, bool overwrite = true, bool xlsx = true)
    {
        if (_workbook == null)
        {
            Workbook = xlsx ? new XSSFWorkbook() : new HSSFWorkbook();
        }

        var sheet = Workbook.NumberOfSheets > sheetIndex ? Workbook.GetSheetAt(sheetIndex) : Workbook.CreateSheet();
        Save(stream, sheet, objects, leaveOpen, overwrite);
    }
    #endregion

    #endregion
}

using BeniceSoft.Core;
using NPOI.SS.UserModel;
using System.Reflection;

namespace BeniceSoft.Office.Excel;

public class ExcelColumn
{
    #region Members
    private ICellStyle? _headerStyle;
    private ICellStyle? _dataStyle;
    private bool _headerStyleCached;
    private bool _dataStyleCached;

    /// <summary>
    /// 列标题
    /// </summary>
    public object? HeaderValue { get; set; }

    /// <summary>
    /// 映射属性信息
    /// </summary>
    public ExcelColumnAttribute Attribute { get; }

    /// <summary>
    /// 最后一个非空值
    /// </summary>
    public object? LastNonBlankValue { get; set; }

    /// <summary>
    /// 当前单元格值
    /// </summary>
    public object? CurrentValue { get; set; }

    /// <summary>
    /// 列标题格式
    /// </summary>
    public short? HeaderFormat { get; set; }

    /// <summary>
    /// 当前单元格格式
    /// </summary>
    public short? DataFormat { get; set; }

    /// <summary>
    /// 与当前行关联的对象
    /// </summary>
    public object? RowTag { get; set; }
    #endregion

    #region Constructors
    public ExcelColumn(object? headerValue, int columnIndex)
    {
        HeaderValue = headerValue;
        Attribute = new ExcelColumnAttribute { Index = columnIndex };
    }

    public ExcelColumn(object? headerValue, int columnIndex, PropertyInfo pi, string hostTypeName, string propertyPath)
    {
        HeaderValue = headerValue;
        Attribute = new ExcelColumnAttribute { Index = columnIndex }.SetProperty(pi, hostTypeName, propertyPath);
    }

    public ExcelColumn(object? headerValue, ExcelColumnAttribute attribute)
    {
        Attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
        HeaderValue = headerValue;
    }
    #endregion

    #region Methods
    public object? RefreshAndGetValue(object? value)
    {
        CurrentValue = value;

        // Specially check for string.
        if ((value as string).IsNull())
        {
            return Attribute.UseLastNonBlankValue == true ? LastNonBlankValue : value;
        }

        LastNonBlankValue = value;

        return value;
    }

    public void SetCellStyle(ICell cell, object? value, bool isHeader, Dictionary<Type, string> defaultFormats, MapHelper helper)
    {
        ArgumentNullException.ThrowIfNull(cell);

        if (isHeader && !_headerStyleCached)
        {
            _headerStyle = helper.GetCellStyle(cell, null, HeaderFormat);

            if (_headerStyle == null && HeaderValue != null)
            {
                _headerStyle = helper.GetDefaultStyle(cell.Sheet.Workbook, HeaderValue, defaultFormats);
            }

            _headerStyleCached = true;
        }
        else if (!isHeader && !_dataStyleCached)
        {
            _dataStyle = helper.GetCellStyle(cell, Attribute.CustomFormat, DataFormat);

            if (_dataStyle == null && value != null)
            {
                _dataStyle = helper.GetDefaultStyle(cell.Sheet.Workbook, value, defaultFormats);
            }

            _dataStyleCached = true;
        }

        cell.CellStyle = isHeader ? _headerStyle : _dataStyle;
    }

    #endregion
}

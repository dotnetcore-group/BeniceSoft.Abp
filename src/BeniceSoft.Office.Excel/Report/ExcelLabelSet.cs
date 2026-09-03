using BeniceSoft.Core;
using BeniceSoft.Core.Reflector;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using System.Collections;
using System.Text.RegularExpressions;

namespace BeniceSoft.Office.Excel.Report;

internal sealed partial class ExcelLabelSet
{
    private readonly List<BaseExcelLabel> _labels = [];
    private readonly ISheet _sheet;

    public IEnumerable<BaseExcelLabel> Labels => _labels;

    public ExcelLabelSet(ISheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        _sheet = sheet;
        for (var rowIndex = 0; rowIndex <= _sheet.LastRowNum; rowIndex++)
        {
            var row = _sheet.GetRow(rowIndex);

            for (var cellIndex = 0; cellIndex < row?.LastCellNum; cellIndex++)
            {
                var cell = row.GetCell(cellIndex);
                if (cell == null)
                {
                    continue;
                }

                if (cell.CellType != CellType.String)
                {
                    continue;
                }

                var label = cell.StringCellValue;
                if (!label.StartsWith('$'))
                {
                    continue;
                }

                if (label.StartsWith("$:"))
                {
                    _labels.Add(new ObjectLabel(label, cell));
                }
                else if (label.StartsWith("$$"))
                {
                    _labels.Add(new TableLabel(label, cell));
                }
                else if (label.StartsWith("$="))
                {
                    _labels.Add(new FormulaLabel(label, cell));
                }
            }
        }
    }

    private static void FillValue(ICell cell, string labelName, object data, IEnumerable<PropertyReflector> properties)
    {
        var prop = properties.FirstOrDefault(x => x.Name == labelName);
        if (prop == null)
        {
            cell.SetCellValue(string.Empty);
            return;
        }

        var value = prop.GetValue(data);
        if (value == null)
        {
            cell.SetCellValue(string.Empty);
            return;
        }

        var type = value.GetType();
        type = type.GetUnderlyingType();

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Empty:
            case TypeCode.DBNull:
                cell.SetCellValue(string.Empty);
                break;
            case TypeCode.Char:
            case TypeCode.String:
                cell.SetCellValue(value.ToString());
                break;
            case TypeCode.Boolean:
                cell.SetCellValue((bool)value);
                break;
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                cell.SetCellValue(Convert.ToDouble(value));
                break;
            case TypeCode.Object:
                {
                    if (value is DateTimeOffset o)
                    {
                        cell.SetCellValue(o.DateTime);
                        break;
                    }

                    if (value is DateOnly date)
                    {
                        cell.SetCellValue(date.ToDateTime());
                        break;
                    }

                    if (value is TimeOnly time)
                    {
                        cell.SetCellValue(time.ToTimeSpan().ToString());
                        break;
                    }

                    cell.SetCellValue(value.ToStringSafe());
                    break;
                }
        }
    }

    private void FillFormula(ICell cell, string formula, Func<BaseExcelLabel, string?>? func = null)
    {
        func ??= lab => lab.AddressString;
        var regex = FormulaRegex();
        var variable = regex.Matches(formula);
        foreach (var match in variable.Cast<Match>())
        {
            var varlable = Labels.FirstOrDefault(x => x.Path == match.Value);
            if (varlable != null)
            {
                formula = formula.Replace($"{{{match.Value}}}", func(varlable) ?? string.Empty);
            }
        }

        cell.SetCellType(CellType.Formula);
        cell.SetCellFormula(formula);
    }

    private void WriteTable(string table, IEnumerable<object?>? datas)
    {
        if (datas.IsNull())
        {
            return;
        }

        var labels = Labels.OfType<TableLabel>().WhereIf(table.IsNotNull(), t => t.Table == table, t => t.Table.IsNull());
        if (labels.IsNull())
        {
            return;
        }

        var row = labels.First().Cell.Row;
        var index = 0;
        var len = datas.Count();
        foreach (var data in datas)
        {
            if (data == null)
            {
                continue;
            }

            var props = data.GetType().GetProperties().FindAll(t => t.CanRead).Select(t => t.GetReflector());
            if (index < len - 1)
            {
                _sheet.CopyRow(row.RowNum, row.RowNum + 1);
            }

            foreach (var label in labels)
            {
                var currentCell = _sheet.GetRow(row.RowNum + index)?.GetCell(label.Cell.ColumnIndex);
                if (currentCell == null)
                {
                    continue;
                }

                if (label.Formula.IsNull())
                {
                    FillValue(currentCell, label.Name, data, props);
                }
                else
                {
                    FillFormula(currentCell, label.Formula, lab => _sheet.GetRow(row.RowNum + index)?.GetCell(lab.Cell.ColumnIndex)?.Address.FormatAsString());
                }
            }

            index++;
        }

        foreach (var label in labels)
        {
            label.CellRange = new CellRangeAddress(row.RowNum, row.RowNum + datas.Count() - 1, label.Cell.ColumnIndex, label.Cell.ColumnIndex);
        }
    }

    private void WriteObject(string name, object? data)
    {
        if (data == null)
        {
            return;
        }

        var props = data.GetType().GetProperties().FindAll(t => t.CanRead);
        if (props.IsNull())
        {
            return;
        }

        var plist = new List<PropertyReflector>();
        foreach (var prop in props)
        {
            if (prop.PropertyType.IsSimpleType())
            {
                plist.Add(prop.GetReflector());
                continue;
            }

            if (prop.PropertyType.IsEnumerableType())
            {
                WriteTable(name.AppendName(prop.Name), (prop.GetReflector().GetValue(data) as IEnumerable)?.Cast<object?>());
                continue;
            }

            WriteObject(name.AppendName(prop.Name), prop.GetReflector().GetValue(data));
        }

        if (plist.IsNotNull())
        {
            var labels = Labels.OfType<ObjectLabel>().WhereIf(name.IsNotNull(), t => t.Object == name, t => t.Object.IsNull());
            if (labels.IsNull())
            {
                return;
            }

            foreach (var label in labels)
            {
                if (label.Formula.IsNull())
                {
                    FillValue(label.Cell, label.Name, data, plist);
                }
                else
                {
                    FillFormula(label.Cell, label.Formula);
                }
            }
        }
    }

    private void WriteFormula()
    {
        var labels = Labels.OfType<FormulaLabel>();
        if (labels.IsNull())
        {
            return;
        }

        foreach (var label in labels)
        {
            FillFormula(label.Cell, label.Formula);
        }
    }

    public void Fill<T>(T data)
    {
        if (data == null)
        {
            return;
        }

        var type = typeof(T);
        if (type.IsEnumerableType())
        {
            WriteTable(string.Empty, (data as IEnumerable)?.Cast<object?>());
        }
        else
        {
            WriteObject(string.Empty, data);
        }

        WriteFormula();
    }

    [GeneratedRegex(@"(?<=[{]).*?(?=[}])")]
    private static partial Regex FormulaRegex();
}

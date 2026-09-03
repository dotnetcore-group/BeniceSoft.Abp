using BeniceSoft.Core;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace BeniceSoft.Office.Excel.Report;

internal abstract class BaseExcelLabel
{
    public BaseExcelLabel(string label, ICell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);

        Cell = cell;
        var nlabel = label[2..];
        if (nlabel.IsNull())
        {
            throw new ArgumentException($"{nameof(BaseExcelLabel)} '{label}' is null");
        }

        var index = nlabel.IndexOf('=');
        if (index >= 0)
        {
            Path = nlabel[..index];
            if (index >= nlabel.Length - 1)
            {
                throw new ArgumentException($"{nameof(BaseExcelLabel)} {nameof(Formula)} is null");
            }

            Formula = nlabel[(index + 1)..];
        }
        else
        {
            Path = nlabel;
        }
    }

    public string Path { get; protected set; } = string.Empty;

    public ICell Cell { get; }

    public string Formula { get; protected set; } = string.Empty;

    public virtual string AddressString => Cell.Address.FormatAsString();
}

/// <summary>
/// start with $$
/// </summary>
internal sealed class TableLabel : BaseExcelLabel
{
    public TableLabel(string label, ICell cell) : base(label, cell)
    {
        var index = Path.LastIndexOf('.');
        if (index > 0)
        {
            Table = Path[..index];
        }

        Name = Path[(index + 1)..];
        if (Name.IsNull())
        {
            throw new ArgumentException($"{nameof(TableLabel)} column '{label}' is null");
        }
    }

    public string Table { get; } = string.Empty;

    public string Name { get; } = string.Empty;

    public CellRangeAddress? CellRange { get; set; }

    public override string AddressString => CellRange?.FormatAsString() ?? base.AddressString;
}

/// <summary>
/// start with $:
/// </summary>
internal sealed class ObjectLabel : BaseExcelLabel
{
    public ObjectLabel(string label, ICell cell) : base(label, cell)
    {
        var index = Path.LastIndexOf('.');
        if (index > 0)
        {
            Object = Path[..index];
        }

        Name = Path[(index + 1)..];
        if (Name.IsNull())
        {
            throw new ArgumentException($"{nameof(ObjectLabel)} column '{label}' is null");
        }
    }

    public string Object { get; } = string.Empty;

    public string Name { get; } = string.Empty;
}

/// <summary>
/// start with $=
/// </summary>
internal sealed class FormulaLabel : BaseExcelLabel
{
    public FormulaLabel(string label, ICell cell) : base(label, cell)
    {
        Formula = label[2..];
        if (Formula.IsNull())
        {
            throw new ArgumentException($"{nameof(FormulaLabel)} '{label}' is null");
        }

        Path = string.Empty;
    }
}

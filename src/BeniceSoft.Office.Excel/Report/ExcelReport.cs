using BeniceSoft.Office.Excel.Report;
using NPOI.SS.UserModel;

namespace BeniceSoft.Office.Excel;

/// <summary>
/// doc to https://github.com/TimChen44/Report.NPOI
/// </summary>
public class ExcelReport
{
    public IWorkbook Workbook { get; set; }

    public ExcelReport(IWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        Workbook = workbook;
    }

    public ExcelReport(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using (stream)
        {
            Workbook = WorkbookFactory.Create(stream);
        }
    }

    public ExcelReport(string filePath) : this(new FileStream(filePath, FileMode.Open))
    {
    }

    public void Save(Stream stream)
    {
        var eva = WorkbookFactory.CreateFormulaEvaluator(Workbook);
        eva.EvaluateAll();
        Workbook.Write(stream, false);
    }

    public void Save(string path)
    {
        using var fs = File.Open(path, FileMode.Create, FileAccess.Write);
        Save(fs);
    }

    public ExcelReport Render<T>(T data, int sheetIndex = 0)
    {
        return Render<T>(Workbook.GetSheetAt(sheetIndex), data);
    }

    public ExcelReport Render<T>(T data, string sheetName)
    {
        return Render<T>(Workbook.GetSheet(sheetName), data);
    }

    private ExcelReport Render<T>(ISheet sheet, T data)
    {
        var set = new ExcelLabelSet(sheet);
        set.Fill<T>(data);
        return this;
    }
}

namespace BeniceSoft.Office.Excel;

public class ExcelRow<T>
{
    public int RowNumber { get; set; }

    public T? Value { get; set; }

    public int ErrorColumnIndex { get; set; } = -1;

    public string? ErrorMessage { get; set; }

    public object? RowTag { get; set; }
}

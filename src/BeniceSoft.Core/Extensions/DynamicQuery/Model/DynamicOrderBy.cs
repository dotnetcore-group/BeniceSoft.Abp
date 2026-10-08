namespace BeniceSoft.Extensions.DynamicQuery;

public class DynamicOrderBy
{
    /// <summary>
    /// 字段名
    /// </summary>
    public string FieldName { get; set; } = "";

    /// <summary>
    /// 排序方向，asc 或 desc, 默认 asc
    /// </summary>
    public string Direction { get; set; } = "asc";
}
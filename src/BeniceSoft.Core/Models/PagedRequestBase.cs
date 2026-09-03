namespace BeniceSoft.Core;

/// <summary>
/// 分页请求基类
/// </summary>
public abstract class PagedRequestBase
{
    /// <summary>
    /// 页码（从1开始）
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// 每页大小
    /// </summary>
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// 关键词搜索
    /// </summary>
    public string? SearchKey { get; set; }
}
using BeniceSoft.Extensions.DynamicQuery;

namespace BeniceSoft.Core;

/// <summary>
/// 分页请求基类
/// </summary>
public abstract class PagedRequestBase : IDynamicOrderByRequest
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

    /// <summary>
    /// 排序字段列表
    /// </summary>
    public List<DynamicOrderBy>? OrderBys { get; set; }
}
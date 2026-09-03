namespace BeniceSoft.Core;

/// <summary>
/// 分页结果
/// </summary>
/// <typeparam name="T"></typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// 数据项
    /// </summary>
    public IReadOnlyList<T> Items { get; set; } = [];

    /// <summary>
    /// 总数
    /// </summary>
    public int TotalCount { get; set; }

    public PagedResult()
    {
    }

    public PagedResult(int totalCount, IEnumerable<T> items)
    {
        TotalCount = totalCount;
        Items = items as IReadOnlyList<T> ?? items.ToList();
    }
}

/// <summary>
/// 分页列表响应结果
/// </summary>
/// <typeparam name="T"></typeparam>
public class PagedList<T> : ResponseResult<PagedResult<T>>
{
    public PagedList()
    {
    }

    public PagedList(int totalCount, IEnumerable<T> items) : base(new PagedResult<T>(totalCount, items))
    {
    }

    public PagedList(int code, string message) : base(code, message)
    {
    }
}

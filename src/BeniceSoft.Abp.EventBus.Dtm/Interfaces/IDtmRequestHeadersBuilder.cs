namespace BeniceSoft.Abp.EventBus.Dtm;

/// <summary>
/// DTM 请求头构建接口，用于自定义 DTM 请求头的构建
/// 实现类可以从当前请求上下文（如 Claims）中获取用户信息并添加到请求头中
/// </summary>
public interface IDtmRequestHeadersBuilder
{
    /// <summary>
    /// 构建请求头
    /// </summary>
    /// <param name="headers">现有的请求头字典，实现类可以向其中添加自定义请求头</param>
    /// <returns>Task</returns>
    Task BuildHeadersAsync(IDictionary<string, string> headers);
}

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public interface ITccBranchCallbackHandler<in TRequest>
{
    /// <summary>
    /// 检查业务条件，预留所需资源
    /// </summary>
    Task TryAsync(TRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 确认执行业务，使用已预留的资源
    /// </summary>
    Task ConfirmAsync(TRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消业务，释放已预留的资源
    /// </summary>
    Task CancelAsync(TRequest request, CancellationToken cancellationToken = default);
}

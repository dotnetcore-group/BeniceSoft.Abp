namespace BeniceSoft.Abp.EventBus.Dtm.Http;

public interface ISagaBranchCallbackHandler<in TRequest>
{
    /// <summary>
    /// 执行业务操作
    /// </summary>
    Task ActionAsync(TRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 撤销业务操作
    /// </summary>
    Task CompensateAsync(TRequest request, CancellationToken cancellationToken = default);
}

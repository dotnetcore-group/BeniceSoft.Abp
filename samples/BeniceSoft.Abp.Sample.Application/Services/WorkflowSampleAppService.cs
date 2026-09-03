using BeniceSoft.Abp.Sample.RemoteService.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.Sample.Application.Services;

/// <summary>
/// 审批流联调示例：通过 IWorkflowCenterClient 远程发起审批
/// </summary>
public class WorkflowSampleAppService : SampleAppServiceBase
{
    private readonly IWorkflowCenterClient _workflowCenterClient;

    public WorkflowSampleAppService(IWorkflowCenterClient workflowCenterClient)
    {
        _workflowCenterClient = workflowCenterClient;
    }

    /// <summary>
    /// 发起审批流（远程调用 WorkflowCenter.TriggerApprovalWorkflow）
    /// </summary>
    /// <param name="form">表单编码或显示名，例如 erp_purchase</param>
    /// <param name="bizId">业务单据 Id</param>
    /// <param name="data">表单数据（可空）</param>
    [HttpPost]
    [UnitOfWork]
    public virtual async Task<long> TriggerApprovalAsync(
        string form,
        long bizId,
        [FromBody] Dictionary<string, object>? data = null)
    {
        return await _workflowCenterClient.TriggerApprovalWorkflowAsync(
            form,
            bizId,
            data ?? new Dictionary<string, object>());
    }
}

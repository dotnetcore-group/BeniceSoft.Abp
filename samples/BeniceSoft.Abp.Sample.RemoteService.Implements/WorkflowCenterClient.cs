using BeniceSoft.Abp.Core.Users;
using BeniceSoft.Abp.Sample.RemoteService.Abstractions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Wecharmer.WorkflowCenter.Application.Contracts.Dtos.Request.ApprovalWorkflows;
using Wecharmer.WorkflowCenter.Application.Contracts.Interfaces;

namespace BeniceSoft.Abp.Sample.RemoteService.Implements;

public class WorkflowCenterClient : IWorkflowCenterClient, ITransientDependency
{
    private readonly IBeniceSoftCurrentUser _currentUser;
    private readonly IApprovalWorkflowAppService _approvalWorkflowAppService;

    public WorkflowCenterClient(IBeniceSoftCurrentUser currentUser, IApprovalWorkflowAppService approvalWorkflowAppService)
    {
        _currentUser = currentUser;
        _approvalWorkflowAppService = approvalWorkflowAppService;
    }

    public async Task<long> TriggerApprovalWorkflowAsync(string form, long bizId, Dictionary<string, object> data)
    {
        data.TryAdd("__submitter__", _currentUser.UserName);
        data.TryAdd("__submitter_department__", _currentUser.DepartmentName);

        var req = new TriggerApprovalWorkflowReqDto
        {
            FormName = form,
            BizId = bizId,
            Data = data,
            SubmitterId = _currentUser.Id ?? 0L,
            SubmitterName = _currentUser.NickName
        };
        var id = await _approvalWorkflowAppService.TriggerApprovalWorkflowAsync(req);
        if (id == default)
        {
            throw new UserFriendlyException($"发起审批失败");
        }

        return id;
    }

    public async Task<bool> PublishEventAsync(string eventName, long approvalWorkflowId, long bizId)
    {
        var response = await _approvalWorkflowAppService.PublishEventAsync(eventName, $"{approvalWorkflowId}:{bizId}");
        return response;
    }
}

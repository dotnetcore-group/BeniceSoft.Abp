namespace BeniceSoft.Abp.Sample.RemoteService.Abstractions;

public interface IWorkflowCenterClient
{
    Task<long> TriggerApprovalWorkflowAsync(string form, long bizId, Dictionary<string, object> data);

    Task<bool> PublishEventAsync(string eventName, long approvalWorkflowId, long bizId);
}

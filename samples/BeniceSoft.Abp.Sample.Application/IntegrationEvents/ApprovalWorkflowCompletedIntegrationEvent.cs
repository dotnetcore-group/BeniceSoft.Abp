using Volo.Abp.EventBus;

namespace BeniceSoft.Abp.Sample.Application.IntegrationEvents;

[EventName("Wecharmer.ApprovalWorkflowCompletedIntegrationEvent")]
public class ApprovalWorkflowCompletedIntegrationEvent
{
    /// <summary>
    /// 表单
    /// </summary>
    public string Form { get; set; } = string.Empty;

    /// <summary>
    /// 审批流id
    /// </summary>
    public long ApprovalWorkflowId { get; set; }

    /// <summary>
    /// 业务id
    /// </summary>
    public long BizId { get; set; }

    /// <summary>
    /// 结果（1：通过 2：拒绝 3：撤回 4: 终止 5: 错误）
    /// </summary>
    public int Result { get; set; }

    /// <summary>
    /// 此流程为子审批流发起时的主审批流id
    /// </summary>
    public long? PredecessorId { get; set; }
}

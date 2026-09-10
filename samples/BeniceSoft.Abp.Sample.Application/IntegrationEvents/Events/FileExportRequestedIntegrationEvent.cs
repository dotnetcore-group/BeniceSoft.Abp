using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.EventBus;

namespace BeniceSoft.Abp.Sample.Application.IntegrationEvents.Events;

[EventName("Wecharmer.FileExportRequestedIntegrationEvent")]
public class FileExportRequestedIntegrationEvent
{
    /// <summary>
    /// 导出任务Id
    /// </summary>
    public long TaskId { get; set; }

    /// <summary>
    /// 租户Id
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// 业务类型
    /// </summary>
    public string BizType { get; set; } = string.Empty;

    /// <summary>
    /// 导出模板的编码
    /// </summary>
    public string TemplateCode { get; set; } = string.Empty;

    /// <summary>
    /// 导出任务标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 查询参数json
    /// </summary>
    public string? ParamsJson { get; set; }
}

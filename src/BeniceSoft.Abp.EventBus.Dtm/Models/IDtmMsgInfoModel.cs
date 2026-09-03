using Volo.Abp.EventBus.Distributed;

namespace BeniceSoft.Abp.EventBus.Dtm;

/// <summary>
/// Msg 消息对象模型接口
/// </summary>
public interface IDtmMsgInfoModel
{
    /// <summary>
    /// 事件是否已添加到待发布任务
    /// </summary>
    bool EventsPublishingActionAdded { get; }

    /// <summary>
    /// 消息Id
    /// </summary>
    string Gid { get; set; }

    /// <summary>
    /// DTM Msg 消息对象
    /// </summary>
    object DtmMessage { get; set; }

    /// <summary>
    /// 当前消息的数据库连接信息
    /// </summary>
    DbConnectionLookupInfoModel DbConnectionLookupInfo { get; }

    /// <summary>
    /// 事件详情
    /// </summary>
    List<OutgoingEventInfo> EventInfos { get; }
}

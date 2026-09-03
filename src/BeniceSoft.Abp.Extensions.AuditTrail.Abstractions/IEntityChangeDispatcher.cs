namespace BeniceSoft.Abp.Extensions.AuditTrail.Abstractions;

/// <summary>
/// 实体变更事件分发接口
/// </summary>
public interface IEntityChangeDispatcher
{
    /// <summary>
    /// 分发实体变更记录
    /// </summary>
    Task DispatchAsync(IReadOnlyList<EntityChangeRecord> changes);
}


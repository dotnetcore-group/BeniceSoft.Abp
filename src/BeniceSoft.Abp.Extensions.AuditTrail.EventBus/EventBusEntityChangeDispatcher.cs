using BeniceSoft.Abp.Extensions.AuditTrail.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace BeniceSoft.Abp.Extensions.AuditTrail.EventBus;

/// <summary>
/// 基于分布式事件总线的实体变更分发器
/// </summary>
public class EventBusEntityChangeDispatcher : IEntityChangeDispatcher, ITransientDependency
{
    private readonly IDistributedEventBus _eventBus;

    public EventBusEntityChangeDispatcher(IDistributedEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public async Task DispatchAsync(IReadOnlyList<EntityChangeRecord> changes)
    {
        foreach (var record in changes)
        {
            var @event = new EntityChangeEvent
            {
                ChangeTime = record.ChangeTime,
                EntityType = record.EntityType,
                EntityId = record.EntityId,
                ChangeType = record.ChangeType,
                OperatorId = record.OperatorId,
                OperatorName = record.OperatorName,
                Changes = record.Changes.Select(c => new PropertyChangeDetail
                {
                    PropertyName = c.PropertyName,
                    DisplayName = c.DisplayName,
                    OriginalValue = c.OriginalValue,
                    NewValue = c.NewValue
                }).ToList()
            };

            await _eventBus.PublishAsync(@event);
        }
    }
}


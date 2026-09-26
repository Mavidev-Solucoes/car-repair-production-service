using Domain.Common;

namespace Domain.Events;

public sealed record WorkOrderCreatedDomainEvent(Guid WorkOrderId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

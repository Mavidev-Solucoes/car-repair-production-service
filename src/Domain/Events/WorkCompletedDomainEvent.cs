using Domain.Common;

namespace Domain.Events;

public sealed record WorkCompletedDomainEvent(Guid WorkOrderId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

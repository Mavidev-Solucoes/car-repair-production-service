using Domain.Common;

namespace Domain.Events;

public sealed record WorkStartedDomainEvent(Guid WorkOrderId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

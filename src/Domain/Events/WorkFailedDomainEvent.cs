using Domain.Common;

namespace Domain.Events;

public sealed record WorkFailedDomainEvent(Guid WorkOrderId, string Reason) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

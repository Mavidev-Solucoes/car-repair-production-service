using MediatR;

namespace Application.WorkOrders.Queries.ListWorkOrders;

public sealed record ListWorkOrdersQuery : IRequest<IReadOnlyCollection<WorkOrderSummaryResponse>>;

public sealed record WorkOrderSummaryResponse(
    Guid Id,
    Guid ServiceJobId,
    string ServiceJobName,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? FailedAtUtc,
    string? FailureReason,
    long Version);

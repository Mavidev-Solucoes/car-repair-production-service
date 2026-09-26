using MediatR;

namespace Application.WorkOrders.Queries.GetWorkOrderById;

public sealed record GetWorkOrderByIdQuery(Guid WorkOrderId) : IRequest<WorkOrderDetailsResponse>;

public sealed record WorkOrderDetailsResponse(
    Guid Id,
    Guid ServiceJobId,
    string ServiceJobName,
    string? ServiceJobDescription,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? FailedAtUtc,
    string? FailureReason,
    long Version,
    IReadOnlyCollection<WorkOrderStatusHistoryResponse> StatusHistory);

public sealed record WorkOrderStatusHistoryResponse(string Status, DateTime ChangedAtUtc, string? Reason);

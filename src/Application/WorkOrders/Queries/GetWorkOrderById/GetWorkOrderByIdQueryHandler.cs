using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;

namespace Application.WorkOrders.Queries.GetWorkOrderById;

public sealed class GetWorkOrderByIdQueryHandler(IServiceOrderJobRepository repository)
    : IRequestHandler<GetWorkOrderByIdQuery, WorkOrderDetailsResponse>
{
    public async Task<WorkOrderDetailsResponse> Handle(GetWorkOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetByIdAsync(request.WorkOrderId, cancellationToken);

        if (workOrder is null)
        {
            throw new NotFoundException($"Work order '{request.WorkOrderId}' was not found.");
        }

        return new WorkOrderDetailsResponse(
            workOrder.Id,
            workOrder.ServiceJob.Id,
            workOrder.ServiceJob.Name,
            workOrder.ServiceJob.Description,
            workOrder.Status.ToString(),
            workOrder.CreatedAtUtc,
            workOrder.StartedAtUtc,
            workOrder.CompletedAtUtc,
            workOrder.FailedAtUtc,
            workOrder.FailureReason,
            workOrder.Version,
            workOrder.StatusHistory
                .Select(history => new WorkOrderStatusHistoryResponse(
                    history.Status.ToString(),
                    history.ChangedAtUtc,
                    history.Reason))
                .ToList());
    }
}

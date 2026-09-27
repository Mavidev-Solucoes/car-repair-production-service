using Application.Common.Interfaces;
using MediatR;

namespace Application.WorkOrders.Queries.ListWorkOrders;

public sealed class ListWorkOrdersQueryHandler(IServiceOrderJobRepository repository)
    : IRequestHandler<ListWorkOrdersQuery, IReadOnlyCollection<WorkOrderSummaryResponse>>
{
    public async Task<IReadOnlyCollection<WorkOrderSummaryResponse>> Handle(ListWorkOrdersQuery request, CancellationToken cancellationToken)
    {
        var workOrders = await repository.ListAsync(cancellationToken);

        return workOrders
            .Select(workOrder => new WorkOrderSummaryResponse(
                workOrder.Id,
                workOrder.ServiceJob.Id,
                workOrder.ServiceJob.Name,
                workOrder.Status.ToString(),
                workOrder.CreatedAtUtc,
                workOrder.StartedAtUtc,
                workOrder.CompletedAtUtc,
                workOrder.FailedAtUtc,
                workOrder.FailureReason,
                workOrder.Version))
            .ToList();
    }
}

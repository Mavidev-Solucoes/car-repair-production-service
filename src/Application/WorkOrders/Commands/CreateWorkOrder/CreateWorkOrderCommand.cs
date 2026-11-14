using MediatR;

namespace Application.WorkOrders.Commands.CreateWorkOrder;

public sealed record CreateWorkOrderCommand(
    Guid ServiceOrderId,
    Guid ServiceJobId,
    string ServiceJobName,
    string? ServiceJobDescription) : IRequest<Guid>;

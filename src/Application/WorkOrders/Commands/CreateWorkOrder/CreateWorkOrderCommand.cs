using MediatR;

namespace Application.WorkOrders.Commands.CreateWorkOrder;

public sealed record CreateWorkOrderCommand(
    Guid ServiceJobId,
    string ServiceJobName,
    string? ServiceJobDescription) : IRequest<Guid>;

using MediatR;

namespace Application.WorkOrders.Commands.CompleteWork;

public sealed record CompleteWorkCommand(Guid WorkOrderId) : IRequest;

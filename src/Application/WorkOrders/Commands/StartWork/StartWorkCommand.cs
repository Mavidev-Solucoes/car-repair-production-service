using MediatR;

namespace Application.WorkOrders.Commands.StartWork;

public sealed record StartWorkCommand(Guid WorkOrderId) : IRequest;

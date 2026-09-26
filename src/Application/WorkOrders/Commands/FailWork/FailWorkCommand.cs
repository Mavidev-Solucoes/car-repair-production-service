using MediatR;

namespace Application.WorkOrders.Commands.FailWork;

public sealed record FailWorkCommand(Guid WorkOrderId, string Reason) : IRequest;

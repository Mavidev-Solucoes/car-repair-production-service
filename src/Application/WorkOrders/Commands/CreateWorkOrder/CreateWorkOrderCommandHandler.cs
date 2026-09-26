using Application.Common.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.WorkOrders.Commands.CreateWorkOrder;

public sealed class CreateWorkOrderCommandHandler(IServiceOrderJobRepository repository)
    : IRequestHandler<CreateWorkOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var serviceJob = new ServiceJob(request.ServiceJobId, request.ServiceJobName, request.ServiceJobDescription);
        var workOrder = ServiceOrderJob.Create(Guid.NewGuid(), serviceJob);

        await repository.AddAsync(workOrder, cancellationToken);

        return workOrder.Id;
    }
}

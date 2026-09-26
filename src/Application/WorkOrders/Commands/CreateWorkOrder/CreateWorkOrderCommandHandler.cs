using Application.Common.Interfaces;
using Application.Common.Messaging;
using Domain.Entities;
using Domain.Events;
using MediatR;

namespace Application.WorkOrders.Commands.CreateWorkOrder;

public sealed class CreateWorkOrderCommandHandler(
    IServiceOrderJobRepository repository,
    IEventPublisher eventPublisher)
    : IRequestHandler<CreateWorkOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var serviceJob = new ServiceJob(request.ServiceJobId, request.ServiceJobName, request.ServiceJobDescription);
        var workOrder = ServiceOrderJob.Create(Guid.NewGuid(), serviceJob);

        await repository.AddAsync(workOrder, cancellationToken);

        var domainEvent = workOrder.DomainEvents.OfType<WorkOrderCreatedDomainEvent>().LastOrDefault();
        if (domainEvent is not null)
        {
            await eventPublisher.PublishDomainEventAsync(domainEvent, workOrder.Id, cancellationToken);
            workOrder.ClearDomainEvents();
        }

        return workOrder.Id;
    }
}

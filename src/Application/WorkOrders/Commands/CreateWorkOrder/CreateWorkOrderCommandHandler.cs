using Application.Common.Interfaces;
using Application.Common.Messaging;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.WorkOrders.Commands.CreateWorkOrder;

public sealed class CreateWorkOrderCommandHandler(
    IServiceOrderJobRepository repository,
    IEventPublisher eventPublisher,
    ILogger<CreateWorkOrderCommandHandler> logger)
    : IRequestHandler<CreateWorkOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var serviceJob = new ServiceJob(request.ServiceJobId, request.ServiceJobName, request.ServiceJobDescription);
        var workOrder = ServiceOrderJob.Create(Guid.NewGuid(), serviceJob);

        await repository.AddAsync(workOrder, cancellationToken);

        var domainEvents = workOrder.DomainEvents.ToArray();
        if (domainEvents.Length > 0)
        {
            try
            {
                await eventPublisher.PublishDomainEventsAsync(domainEvents, workOrder.Id, cancellationToken);
                workOrder.ClearDomainEvents();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish one or more events for work order {WorkOrderId}.", workOrder.Id);
            }
        }

        return workOrder.Id;
    }
}

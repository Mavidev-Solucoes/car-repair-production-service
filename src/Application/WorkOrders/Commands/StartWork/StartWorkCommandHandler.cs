using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Messaging;
using Domain.Events;
using MediatR;

namespace Application.WorkOrders.Commands.StartWork;

public sealed class StartWorkCommandHandler(
    IServiceOrderJobRepository repository,
    IEventPublisher eventPublisher)
    : IRequestHandler<StartWorkCommand>
{
    public async Task<Unit> Handle(StartWorkCommand request, CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetByIdAsync(request.WorkOrderId, cancellationToken);

        if (workOrder is null)
        {
            throw new NotFoundException($"Work order '{request.WorkOrderId}' was not found.");
        }

        try
        {
            workOrder.StartWork();
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await repository.UpdateAsync(workOrder, cancellationToken);

        var domainEvent = workOrder.DomainEvents.OfType<WorkStartedDomainEvent>().LastOrDefault();
        if (domainEvent is not null)
        {
            await eventPublisher.PublishDomainEventAsync(domainEvent, workOrder.Id, cancellationToken);
            workOrder.ClearDomainEvents();
        }

        return Unit.Value;
    }
}

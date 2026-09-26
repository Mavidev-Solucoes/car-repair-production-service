using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Messaging;
using Domain.Events;
using MediatR;

namespace Application.WorkOrders.Commands.FailWork;

public sealed class FailWorkCommandHandler(
    IServiceOrderJobRepository repository,
    IEventPublisher eventPublisher)
    : IRequestHandler<FailWorkCommand>
{
    public async Task<Unit> Handle(FailWorkCommand request, CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetByIdAsync(request.WorkOrderId, cancellationToken);

        if (workOrder is null)
        {
            throw new NotFoundException($"Work order '{request.WorkOrderId}' was not found.");
        }

        try
        {
            workOrder.FailWork(request.Reason);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await repository.UpdateAsync(workOrder, cancellationToken);

        var domainEvent = workOrder.DomainEvents.OfType<WorkFailedDomainEvent>().LastOrDefault();
        if (domainEvent is not null)
        {
            await eventPublisher.PublishDomainEventAsync(domainEvent, workOrder.Id, cancellationToken);
            workOrder.ClearDomainEvents();
        }

        return Unit.Value;
    }
}

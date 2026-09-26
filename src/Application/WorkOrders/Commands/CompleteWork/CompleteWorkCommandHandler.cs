using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Messaging;
using Domain.Events;
using MediatR;

namespace Application.WorkOrders.Commands.CompleteWork;

public sealed class CompleteWorkCommandHandler(
    IServiceOrderJobRepository repository,
    IEventPublisher eventPublisher)
    : IRequestHandler<CompleteWorkCommand>
{
    public async Task<Unit> Handle(CompleteWorkCommand request, CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetByIdAsync(request.WorkOrderId, cancellationToken);

        if (workOrder is null)
        {
            throw new NotFoundException($"Work order '{request.WorkOrderId}' was not found.");
        }

        try
        {
            workOrder.CompleteWork();
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await repository.UpdateAsync(workOrder, cancellationToken);

        var domainEvent = workOrder.DomainEvents.OfType<WorkCompletedDomainEvent>().LastOrDefault();
        if (domainEvent is not null)
        {
            await eventPublisher.PublishDomainEventAsync(domainEvent, workOrder.Id, cancellationToken);
            workOrder.ClearDomainEvents();
        }

        return Unit.Value;
    }
}

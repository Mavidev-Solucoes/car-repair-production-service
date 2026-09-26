using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.WorkOrders.Commands.CompleteWork;

public sealed class CompleteWorkCommandHandler(
    IServiceOrderJobRepository repository,
    IEventPublisher eventPublisher,
    ILogger<CompleteWorkCommandHandler> logger)
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

        return Unit.Value;
    }
}

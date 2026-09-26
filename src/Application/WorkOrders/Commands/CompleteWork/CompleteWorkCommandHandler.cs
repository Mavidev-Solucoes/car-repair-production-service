using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;

namespace Application.WorkOrders.Commands.CompleteWork;

public sealed class CompleteWorkCommandHandler(IServiceOrderJobRepository repository)
    : IRequestHandler<CompleteWorkCommand>
{
    public async Task Handle(CompleteWorkCommand request, CancellationToken cancellationToken)
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
    }
}

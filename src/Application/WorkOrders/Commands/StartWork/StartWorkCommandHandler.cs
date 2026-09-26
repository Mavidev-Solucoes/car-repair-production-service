using Application.Common.Exceptions;
using Application.Common.Interfaces;
using MediatR;

namespace Application.WorkOrders.Commands.StartWork;

public sealed class StartWorkCommandHandler(IServiceOrderJobRepository repository)
    : IRequestHandler<StartWorkCommand>
{
    public async Task Handle(StartWorkCommand request, CancellationToken cancellationToken)
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
    }
}

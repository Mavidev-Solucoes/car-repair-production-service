using Application.WorkOrders.Commands.CompleteWork;
using Application.WorkOrders.Commands.CreateWorkOrder;
using Application.WorkOrders.Commands.FailWork;
using Application.WorkOrders.Commands.StartWork;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/work-orders")]
public sealed class WorkOrdersController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateWorkOrder([FromBody] CreateWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var workOrderId = await sender.Send(
            new CreateWorkOrderCommand(request.ServiceJobId, request.ServiceJobName, request.ServiceJobDescription),
            cancellationToken);

        return CreatedAtAction(nameof(CreateWorkOrder), new { id = workOrderId }, new { id = workOrderId });
    }

    [HttpPost("{workOrderId:guid}/start")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> StartWork(Guid workOrderId, CancellationToken cancellationToken)
    {
        await sender.Send(new StartWorkCommand(workOrderId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{workOrderId:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CompleteWork(Guid workOrderId, CancellationToken cancellationToken)
    {
        await sender.Send(new CompleteWorkCommand(workOrderId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{workOrderId:guid}/fail")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> FailWork(Guid workOrderId, [FromBody] FailWorkRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new FailWorkCommand(workOrderId, request.Reason), cancellationToken);
        return NoContent();
    }
}

public sealed record CreateWorkOrderRequest(Guid ServiceJobId, string ServiceJobName, string? ServiceJobDescription);

public sealed record FailWorkRequest(string Reason);

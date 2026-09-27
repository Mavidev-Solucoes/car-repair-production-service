using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.WorkOrders.Queries.GetWorkOrder;
using Domain.Entities;
using Xunit;

namespace UnitTests;

public sealed class GetWorkOrderQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReturnOrderedStatusHistory()
    {
        var workOrder = ServiceOrderJob.Create(Guid.NewGuid(), new ServiceJob(Guid.NewGuid(), "Alignment", "Wheel alignment"));
        workOrder.StartWork();
        workOrder.FailWork("Part unavailable");

        var handler = new GetWorkOrderQueryHandler(new InMemoryServiceOrderJobRepository(workOrder));

        var result = await handler.Handle(new GetWorkOrderQuery(workOrder.Id), CancellationToken.None);

        Assert.Equal(workOrder.Id, result.Id);
        Assert.Equal("Failed", result.Status);
        Assert.Equal(3, result.StatusHistory.Count);
        Assert.Equal(["Pending", "InProgress", "Failed"], result.StatusHistory.Select(item => item.Status).ToArray());
    }

    [Fact]
    public async Task Handle_WhenWorkOrderDoesNotExist_ShouldThrow()
    {
        var handler = new GetWorkOrderQueryHandler(new InMemoryServiceOrderJobRepository(null));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetWorkOrderQuery(Guid.NewGuid()), CancellationToken.None));
    }

    private sealed class InMemoryServiceOrderJobRepository(ServiceOrderJob? item) : IServiceOrderJobRepository
    {
        public Task AddAsync(ServiceOrderJob serviceOrderJob, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<ServiceOrderJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(item?.Id == id ? item : null);

        public Task<IReadOnlyCollection<ServiceOrderJob>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ServiceOrderJob>>([]);

        public Task UpdateAsync(ServiceOrderJob serviceOrderJob, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

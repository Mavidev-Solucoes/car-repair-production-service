using Application.Common.Interfaces;
using Application.WorkOrders.Queries.ListWorkOrders;
using Domain.Entities;
using Xunit;

namespace UnitTests;

public sealed class ListWorkOrdersQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldMapRepositoryItemsToSummaryResponse()
    {
        var newest = ServiceOrderJob.Create(Guid.NewGuid(), new ServiceJob(Guid.NewGuid(), "Alignment", "Wheel alignment"));
        newest.StartWork();

        var oldest = ServiceOrderJob.Create(Guid.NewGuid(), new ServiceJob(Guid.NewGuid(), "Oil Change", "Replace engine oil"));

        var repository = new InMemoryServiceOrderJobRepository([newest, oldest]);
        var handler = new ListWorkOrdersQueryHandler(repository);

        var result = await handler.Handle(new ListWorkOrdersQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(newest.Id, result.First().Id);
        Assert.Equal("Alignment", result.First().ServiceJobName);
        Assert.Equal("InProgress", result.First().Status);
    }

    private sealed class InMemoryServiceOrderJobRepository(IReadOnlyCollection<ServiceOrderJob> items) : IServiceOrderJobRepository
    {
        public Task AddAsync(ServiceOrderJob serviceOrderJob, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<ServiceOrderJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<ServiceOrderJob?>(null);

        public Task<IReadOnlyCollection<ServiceOrderJob>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(items);

        public Task UpdateAsync(ServiceOrderJob serviceOrderJob, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

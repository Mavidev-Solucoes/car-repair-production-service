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
        newest.FailWork("Part unavailable");

        var oldest = ServiceOrderJob.Create(Guid.NewGuid(), new ServiceJob(Guid.NewGuid(), "Oil Change", "Replace engine oil"));
        oldest.StartWork();
        oldest.CompleteWork();

        var repository = new InMemoryServiceOrderJobRepository([newest, oldest]);
        var handler = new ListWorkOrdersQueryHandler(repository);

        var result = await handler.Handle(new ListWorkOrdersQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(newest.Id, result.First().Id);
        Assert.Equal("Alignment", result.First().ServiceJobName);
        Assert.Equal("Failed", result.First().Status);
        Assert.Equal(newest.ServiceJob.Id, result.First().ServiceJobId);
        Assert.Equal(newest.CreatedAtUtc, result.First().CreatedAtUtc);
        Assert.Equal(newest.StartedAtUtc, result.First().StartedAtUtc);
        Assert.Null(result.First().CompletedAtUtc);
        Assert.Equal(newest.FailedAtUtc, result.First().FailedAtUtc);
        Assert.Equal("Part unavailable", result.First().FailureReason);
        Assert.Equal(newest.Version, result.First().Version);

        Assert.Equal(oldest.Id, result.Last().Id);
        Assert.Equal("Completed", result.Last().Status);
        Assert.Equal(oldest.CreatedAtUtc, result.Last().CreatedAtUtc);
        Assert.Equal(oldest.StartedAtUtc, result.Last().StartedAtUtc);
        Assert.Equal(oldest.CompletedAtUtc, result.Last().CompletedAtUtc);
        Assert.Null(result.Last().FailedAtUtc);
        Assert.Null(result.Last().FailureReason);
        Assert.Equal(oldest.Version, result.Last().Version);
    }

    private sealed class InMemoryServiceOrderJobRepository(IReadOnlyCollection<ServiceOrderJob> items) : IServiceOrderJobRepository
    {
        public Task AddAsync(ServiceOrderJob serviceOrderJob, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<ServiceOrderJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<ServiceOrderJob?>(null);

        public Task<IReadOnlyCollection<ServiceOrderJob>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(items);

        public Task UpdateAsync(ServiceOrderJob serviceOrderJob, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

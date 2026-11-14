using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace InfrastructureTests;

[Collection(InfrastructureCollection.Name)]
public sealed class ServiceOrderJobRepositoryTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task AddAndQueries_ShouldRoundTripTheAggregate()
    {
        await using var provider = fixture.BuildServices(NewDatabase());
        var repository = Resolve(provider);
        var first = NewJob("Troca de óleo");
        await repository.AddAsync(first, CancellationToken.None);
        await Task.Delay(20);
        var second = NewJob("Alinhamento");
        await repository.AddAsync(second, CancellationToken.None);

        var byId = await repository.GetByIdAsync(first.Id, CancellationToken.None);
        var byServiceOrder = await repository.GetByServiceOrderIdAsync(second.ServiceOrderId, CancellationToken.None);
        var all = await repository.ListAsync(CancellationToken.None);

        Assert.NotNull(byId);
        Assert.Equal(first.ServiceOrderId, byId.ServiceOrderId);
        Assert.Equal("Troca de óleo", byId.ServiceJob.Name);
        Assert.Equal(ServiceOrderJobStatus.Pending, byId.Status);
        Assert.Single(byId.StatusHistory);
        Assert.Equal(second.Id, byServiceOrder?.Id);
        Assert.Equal([second.Id, first.Id], all.Select(job => job.Id).ToArray());
        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Null(await repository.GetByServiceOrderIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Update_ShouldPersistTransitionsAndHistory()
    {
        await using var provider = fixture.BuildServices(NewDatabase());
        var repository = Resolve(provider);
        var job = NewJob("Freios");
        await repository.AddAsync(job, CancellationToken.None);

        var loaded = (await repository.GetByIdAsync(job.Id, CancellationToken.None))!;
        loaded.StartWork();
        await repository.UpdateAsync(loaded, CancellationToken.None);

        var started = (await repository.GetByIdAsync(job.Id, CancellationToken.None))!;
        started.FailWork("  Peça em falta  ");
        await repository.UpdateAsync(started, CancellationToken.None);

        var failed = (await repository.GetByIdAsync(job.Id, CancellationToken.None))!;
        Assert.Equal(ServiceOrderJobStatus.Failed, failed.Status);
        Assert.Equal("Peça em falta", failed.FailureReason);
        Assert.NotNull(failed.StartedAtUtc);
        Assert.NotNull(failed.FailedAtUtc);
        Assert.Equal(2, failed.Version);
        Assert.Equal(
            [ServiceOrderJobStatus.Pending, ServiceOrderJobStatus.InProgress, ServiceOrderJobStatus.Failed],
            failed.StatusHistory.Select(history => history.Status).ToArray());
        Assert.Equal("Peça em falta", failed.StatusHistory.Last().Reason);
    }

    [Fact]
    public async Task Update_WithStaleVersion_ShouldThrowBusinessRule()
    {
        await using var provider = fixture.BuildServices(NewDatabase());
        var repository = Resolve(provider);
        var job = NewJob("Suspensão");
        await repository.AddAsync(job, CancellationToken.None);

        var copyA = (await repository.GetByIdAsync(job.Id, CancellationToken.None))!;
        var copyB = (await repository.GetByIdAsync(job.Id, CancellationToken.None))!;
        copyA.StartWork();
        copyB.StartWork();
        await repository.UpdateAsync(copyA, CancellationToken.None);

        await Assert.ThrowsAsync<BusinessRuleException>(() => repository.UpdateAsync(copyB, CancellationToken.None));
    }

    [Fact]
    public async Task Update_WhenMissing_ShouldThrowNotFound()
    {
        await using var provider = fixture.BuildServices(NewDatabase());
        var repository = Resolve(provider);
        var job = NewJob("Inexistente");
        job.StartWork();

        await Assert.ThrowsAsync<NotFoundException>(() => repository.UpdateAsync(job, CancellationToken.None));
    }

    [Fact]
    public async Task Add_WithDuplicatedServiceOrder_ShouldBeRejectedByUniqueIndex()
    {
        await using var provider = fixture.BuildServices(NewDatabase());
        var repository = Resolve(provider);
        var serviceOrderId = Guid.NewGuid();

        await repository.AddAsync(ServiceOrderJob.Create(Guid.NewGuid(), serviceOrderId, new ServiceJob(Guid.NewGuid(), "A", null)), CancellationToken.None);

        await Assert.ThrowsAsync<MongoWriteException>(() => repository.AddAsync(
            ServiceOrderJob.Create(Guid.NewGuid(), serviceOrderId, new ServiceJob(Guid.NewGuid(), "B", null)),
            CancellationToken.None));
    }

    private static IServiceOrderJobRepository Resolve(ServiceProvider provider)
        => provider.CreateScope().ServiceProvider.GetRequiredService<IServiceOrderJobRepository>();

    private static ServiceOrderJob NewJob(string name)
        => ServiceOrderJob.Create(Guid.NewGuid(), Guid.NewGuid(), new ServiceJob(Guid.NewGuid(), name, $"{name} completa"));

    private static string NewDatabase() => $"production_tests_{Guid.NewGuid():N}";
}

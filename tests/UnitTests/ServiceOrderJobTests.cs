using Domain.Entities;
using Domain.Enums;
using Domain.Events;

namespace UnitTests;

public sealed class ServiceOrderJobTests
{
    [Fact]
    public void Create_ShouldInitializePendingStatusAndHistory()
    {
        var serviceJob = new ServiceJob(Guid.NewGuid(), "Oil Change", "Replace engine oil");

        var workOrder = ServiceOrderJob.Create(Guid.NewGuid(), serviceJob);

        Assert.Equal(ServiceOrderJobStatus.Pending, workOrder.Status);
        Assert.Single(workOrder.StatusHistory);
        Assert.Contains(workOrder.DomainEvents, domainEvent => domainEvent is WorkOrderCreatedDomainEvent);
    }

    [Fact]
    public void StartWork_ShouldChangeStatusAndRaiseWorkStartedEvent()
    {
        var serviceJob = new ServiceJob(Guid.NewGuid(), "Oil Change", "Replace engine oil");
        var workOrder = ServiceOrderJob.Create(Guid.NewGuid(), serviceJob);

        workOrder.StartWork();

        Assert.Equal(ServiceOrderJobStatus.InProgress, workOrder.Status);
        Assert.NotNull(workOrder.StartedAtUtc);
        Assert.Contains(workOrder.DomainEvents, domainEvent => domainEvent is WorkStartedDomainEvent);
    }

    [Fact]
    public void CompleteWork_WhenNotInProgress_ShouldThrow()
    {
        var serviceJob = new ServiceJob(Guid.NewGuid(), "Oil Change", "Replace engine oil");
        var workOrder = ServiceOrderJob.Create(Guid.NewGuid(), serviceJob);

        var action = () => workOrder.CompleteWork();

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void FailWork_ShouldSetFailureReasonAndRaiseEvent()
    {
        var serviceJob = new ServiceJob(Guid.NewGuid(), "Oil Change", "Replace engine oil");
        var workOrder = ServiceOrderJob.Create(Guid.NewGuid(), serviceJob);
        workOrder.StartWork();

        workOrder.FailWork("Missing spare part");

        Assert.Equal(ServiceOrderJobStatus.Failed, workOrder.Status);
        Assert.Equal("Missing spare part", workOrder.FailureReason);
        Assert.NotNull(workOrder.FailedAtUtc);
        Assert.Contains(workOrder.DomainEvents, domainEvent => domainEvent is WorkFailedDomainEvent);
    }
}

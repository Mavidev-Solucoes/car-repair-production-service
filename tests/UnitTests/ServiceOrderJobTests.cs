using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Xunit;

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
        Assert.Equal(3, workOrder.StatusHistory.Count);
        Assert.Equal(ServiceOrderJobStatus.Failed, workOrder.StatusHistory.Last().Status);
        Assert.Equal("Missing spare part", workOrder.StatusHistory.Last().Reason);
        Assert.Contains(workOrder.DomainEvents, domainEvent => domainEvent is WorkFailedDomainEvent);
    }

    [Fact]
    public void ServiceJob_ShouldTrimAndValidateInputs()
    {
        var serviceJob = new ServiceJob(Guid.NewGuid(), "  Oil Change  ", "  Replace engine oil  ");
        var serviceJobWithoutDescription = new ServiceJob(Guid.NewGuid(), "Inspection", "   ");

        Assert.Equal("Oil Change", serviceJob.Name);
        Assert.Equal("Replace engine oil", serviceJob.Description);
        Assert.Null(serviceJobWithoutDescription.Description);
        Assert.Throws<ArgumentException>(() => new ServiceJob(Guid.Empty, "Oil Change", null));
        Assert.Throws<ArgumentException>(() => new ServiceJob(Guid.NewGuid(), " ", null));
        Assert.Throws<ArgumentException>(() => new ServiceJob(Guid.NewGuid(), "Oil Change", new string('a', ServiceJob.DescriptionMaxLength + 1)));
    }

    [Fact]
    public void FailWork_WhenReasonIsBlank_ShouldThrow()
    {
        var serviceJob = new ServiceJob(Guid.NewGuid(), "Oil Change", "Replace engine oil");
        var workOrder = ServiceOrderJob.Create(Guid.NewGuid(), serviceJob);
        workOrder.StartWork();

        var action = () => workOrder.FailWork("   ");

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void FailWork_ShouldTrimReasonAndValidateMaxLength()
    {
        var serviceJob = new ServiceJob(Guid.NewGuid(), "Oil Change", "Replace engine oil");
        var workOrder = ServiceOrderJob.Create(Guid.NewGuid(), serviceJob);
        workOrder.StartWork();

        workOrder.FailWork("  Missing spare part  ");

        Assert.Equal("Missing spare part", workOrder.FailureReason);
        Assert.Throws<ArgumentException>(() =>
        {
            var anotherWorkOrder = ServiceOrderJob.Create(Guid.NewGuid(), serviceJob);
            anotherWorkOrder.StartWork();
            anotherWorkOrder.FailWork(new string('a', ServiceOrderJobStatusHistory.ReasonMaxLength + 1));
        });
    }
}

using Domain.Common;
using Domain.Enums;
using Domain.Events;

namespace Domain.Entities;

public sealed class ServiceOrderJob : AggregateRoot
{
    private readonly List<ServiceOrderJobStatusHistory> _statusHistory = [];

    private ServiceOrderJob()
    {
    }

    private ServiceOrderJob(Guid id, ServiceJob serviceJob)
    {
        Id = id;
        ServiceJob = serviceJob;
        Status = ServiceOrderJobStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
        AddStatusHistory(ServiceOrderJobStatus.Pending);
        AddDomainEvent(new WorkOrderCreatedDomainEvent(Id));
    }

    public Guid Id { get; private set; }

    public ServiceJob ServiceJob { get; private set; } = null!;

    public ServiceOrderJobStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public string? FailureReason { get; private set; }

    public IReadOnlyCollection<ServiceOrderJobStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public static ServiceOrderJob Create(Guid id, ServiceJob serviceJob)
    {
        return new ServiceOrderJob(id, serviceJob);
    }

    public static ServiceOrderJob Rehydrate(
        Guid id,
        ServiceJob serviceJob,
        ServiceOrderJobStatus status,
        DateTime createdAtUtc,
        DateTime? startedAtUtc,
        DateTime? completedAtUtc,
        string? failureReason,
        IReadOnlyCollection<ServiceOrderJobStatusHistory> statusHistory)
    {
        var workOrder = new ServiceOrderJob
        {
            Id = id,
            ServiceJob = serviceJob,
            Status = status,
            CreatedAtUtc = createdAtUtc,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            FailureReason = failureReason
        };

        workOrder._statusHistory.AddRange(statusHistory);

        return workOrder;
    }

    public void StartWork()
    {
        if (Status != ServiceOrderJobStatus.Pending)
        {
            throw new InvalidOperationException("Only pending work orders can be started.");
        }

        Status = ServiceOrderJobStatus.InProgress;
        StartedAtUtc = DateTime.UtcNow;
        AddStatusHistory(Status);
        AddDomainEvent(new WorkStartedDomainEvent(Id));
    }

    public void CompleteWork()
    {
        if (Status != ServiceOrderJobStatus.InProgress)
        {
            throw new InvalidOperationException("Only in-progress work orders can be completed.");
        }

        Status = ServiceOrderJobStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        AddStatusHistory(Status);
        AddDomainEvent(new WorkCompletedDomainEvent(Id));
    }

    public void FailWork(string reason)
    {
        if (Status != ServiceOrderJobStatus.InProgress)
        {
            throw new InvalidOperationException("Only in-progress work orders can be failed.");
        }

        FailureReason = reason;
        Status = ServiceOrderJobStatus.Failed;
        CompletedAtUtc = DateTime.UtcNow;
        AddStatusHistory(Status, reason);
        AddDomainEvent(new WorkFailedDomainEvent(Id, reason));
    }

    private void AddStatusHistory(ServiceOrderJobStatus status, string? reason = null)
    {
        _statusHistory.Add(new ServiceOrderJobStatusHistory(status, DateTime.UtcNow, reason));
    }
}

using Domain.Enums;

namespace Domain.Entities;

public sealed class ServiceOrderJobStatusHistory
{
    private ServiceOrderJobStatusHistory()
    {
    }

    public ServiceOrderJobStatusHistory(ServiceOrderJobStatus status, DateTime changedAtUtc, string? reason = null)
    {
        Status = status;
        ChangedAtUtc = changedAtUtc;
        Reason = reason;
    }

    public ServiceOrderJobStatus Status { get; private set; }

    public DateTime ChangedAtUtc { get; private set; }

    public string? Reason { get; private set; }
}

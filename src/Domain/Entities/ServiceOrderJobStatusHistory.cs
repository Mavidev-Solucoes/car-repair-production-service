using Domain.Enums;

namespace Domain.Entities;

public sealed class ServiceOrderJobStatusHistory
{
    public const int ReasonMaxLength = 500;

    private ServiceOrderJobStatusHistory()
    {
    }

    public ServiceOrderJobStatusHistory(ServiceOrderJobStatus status, DateTime changedAtUtc, string? reason = null)
    {
        Status = status;
        ChangedAtUtc = changedAtUtc;
        Reason = NormalizeReason(reason);
    }

    public ServiceOrderJobStatus Status { get; private set; }

    public DateTime ChangedAtUtc { get; private set; }

    public string? Reason { get; private set; }

    private static string? NormalizeReason(string? reason)
    {
        if (reason is null)
        {
            return null;
        }

        var normalizedReason = reason.Trim();
        if (normalizedReason.Length == 0)
        {
            return null;
        }

        if (normalizedReason.Length > ReasonMaxLength)
        {
            throw new ArgumentException($"Reason cannot exceed {ReasonMaxLength} characters.", nameof(reason));
        }

        return normalizedReason;
    }
}

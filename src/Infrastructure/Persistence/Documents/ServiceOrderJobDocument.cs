namespace Infrastructure.Persistence.Documents;

internal sealed class ServiceOrderJobDocument
{
    public Guid Id { get; set; }

    public ServiceJobDocument ServiceJob { get; set; } = null!;

    public int Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string? FailureReason { get; set; }

    public List<ServiceOrderJobStatusHistoryDocument> StatusHistory { get; set; } = [];
}

internal sealed class ServiceJobDocument
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

internal sealed class ServiceOrderJobStatusHistoryDocument
{
    public int Status { get; set; }

    public DateTime ChangedAtUtc { get; set; }

    public string? Reason { get; set; }
}

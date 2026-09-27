using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Documents;

namespace Infrastructure.Persistence.Mappings;

internal static class ServiceOrderJobMapper
{
    public static ServiceOrderJobDocument ToDocument(ServiceOrderJob entity)
    {
        return new ServiceOrderJobDocument
        {
            Id = entity.Id,
            ServiceJob = new ServiceJobDocument
            {
                Id = entity.ServiceJob.Id,
                Name = entity.ServiceJob.Name,
                Description = entity.ServiceJob.Description
            },
            Status = (int)entity.Status,
            CreatedAtUtc = entity.CreatedAtUtc,
            StartedAtUtc = entity.StartedAtUtc,
            CompletedAtUtc = entity.CompletedAtUtc,
            FailedAtUtc = entity.FailedAtUtc,
            FailureReason = entity.FailureReason,
            Version = entity.Version,
            StatusHistory = entity.StatusHistory
                .Select(history => new ServiceOrderJobStatusHistoryDocument
                {
                    Status = (int)history.Status,
                    ChangedAtUtc = history.ChangedAtUtc,
                    Reason = history.Reason
                })
                .ToList()
        };
    }

    public static ServiceOrderJob ToEntity(ServiceOrderJobDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.ServiceJob is null)
        {
            throw new InvalidOperationException("Persisted work order is missing its service job payload.");
        }

        return ServiceOrderJob.Rehydrate(
            document.Id,
            new ServiceJob(document.ServiceJob.Id, document.ServiceJob.Name, document.ServiceJob.Description),
            (ServiceOrderJobStatus)document.Status,
            document.CreatedAtUtc,
            document.StartedAtUtc,
            document.CompletedAtUtc,
            document.FailedAtUtc,
            document.FailureReason,
            document.Version,
            (document.StatusHistory ?? [])
                .Select(history => new ServiceOrderJobStatusHistory(
                    (ServiceOrderJobStatus)history.Status,
                    history.ChangedAtUtc,
                    history.Reason))
                .ToList());
    }
}

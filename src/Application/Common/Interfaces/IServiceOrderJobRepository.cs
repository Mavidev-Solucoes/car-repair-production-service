using Domain.Entities;

namespace Application.Common.Interfaces;

public interface IServiceOrderJobRepository
{
    Task AddAsync(ServiceOrderJob serviceOrderJob, CancellationToken cancellationToken);

    Task<ServiceOrderJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task UpdateAsync(ServiceOrderJob serviceOrderJob, CancellationToken cancellationToken);
}

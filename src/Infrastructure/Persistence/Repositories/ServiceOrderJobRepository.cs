using Application.Common.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Documents;
using Infrastructure.Persistence.Mappings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

internal sealed class ServiceOrderJobRepository(
    IMongoClient mongoClient,
    IOptions<MongoDbSettings> mongoDbSettings,
    IDomainEventDispatcher domainEventDispatcher) : IServiceOrderJobRepository
{
    private readonly IMongoCollection<ServiceOrderJobDocument> _collection = mongoClient
        .GetDatabase(mongoDbSettings.Value.DatabaseName)
        .GetCollection<ServiceOrderJobDocument>(mongoDbSettings.Value.ServiceOrderJobsCollectionName);

    public async Task AddAsync(ServiceOrderJob serviceOrderJob, CancellationToken cancellationToken)
    {
        var document = ServiceOrderJobMapper.ToDocument(serviceOrderJob);

        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        await DispatchDomainEventsAsync(serviceOrderJob, cancellationToken);
    }

    public async Task<ServiceOrderJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(item => item.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : ServiceOrderJobMapper.ToEntity(document);
    }

    public async Task UpdateAsync(ServiceOrderJob serviceOrderJob, CancellationToken cancellationToken)
    {
        var document = ServiceOrderJobMapper.ToDocument(serviceOrderJob);

        await _collection.ReplaceOneAsync(item => item.Id == serviceOrderJob.Id, document, cancellationToken: cancellationToken);
        await DispatchDomainEventsAsync(serviceOrderJob, cancellationToken);
    }

    private async Task DispatchDomainEventsAsync(ServiceOrderJob serviceOrderJob, CancellationToken cancellationToken)
    {
        if (serviceOrderJob.DomainEvents.Count == 0)
        {
            return;
        }

        await domainEventDispatcher.DispatchAsync(serviceOrderJob.DomainEvents, cancellationToken);
        serviceOrderJob.ClearDomainEvents();
    }
}

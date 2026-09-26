using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Documents;
using Infrastructure.Persistence.Mappings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Repositories;

internal sealed class ServiceOrderJobRepository(
    IMongoClient mongoClient,
    IOptions<MongoDbSettings> mongoDbSettings) : IServiceOrderJobRepository
{
    private readonly IMongoCollection<ServiceOrderJobDocument> _collection = mongoClient
        .GetDatabase(mongoDbSettings.Value.DatabaseName)
        .GetCollection<ServiceOrderJobDocument>(mongoDbSettings.Value.ServiceOrderJobsCollectionName);

    public async Task AddAsync(ServiceOrderJob serviceOrderJob, CancellationToken cancellationToken)
    {
        var document = ServiceOrderJobMapper.ToDocument(serviceOrderJob);

        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        serviceOrderJob.ClearDomainEvents();
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
        var expectedVersion = serviceOrderJob.Version - 1;

        var result = await _collection.ReplaceOneAsync(
            item => item.Id == serviceOrderJob.Id && item.Version == expectedVersion,
            document,
            cancellationToken: cancellationToken);

        if (result.MatchedCount == 0)
        {
            var exists = await _collection
                .Find(item => item.Id == serviceOrderJob.Id)
                .AnyAsync(cancellationToken);

            if (!exists)
            {
                throw new NotFoundException($"Work order '{serviceOrderJob.Id}' was not found.");
            }

            throw new BusinessRuleException("Work order state changed by another request. Reload and retry.");
        }

        serviceOrderJob.ClearDomainEvents();
    }
}

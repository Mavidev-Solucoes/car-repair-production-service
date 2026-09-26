using Infrastructure.Persistence.Documents;
using Infrastructure.Persistence.Mappings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

internal sealed class MongoDbContext : IMongoDbContext
{
    public MongoDbContext(
        IMongoClient mongoClient,
        IOptions<MongoDbSettings> mongoDbSettings,
        IEnumerable<IMongoCollectionMapping> collectionMappings)
    {
        foreach (var collectionMapping in collectionMappings)
        {
            collectionMapping.Configure();
        }

        var database = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        ServiceOrderJobs = database.GetCollection<ServiceOrderJobDocument>(mongoDbSettings.Value.ServiceOrderJobsCollectionName);
    }

    public IMongoCollection<ServiceOrderJobDocument> ServiceOrderJobs { get; }
}

using Infrastructure.Persistence.Documents;
using Infrastructure.Persistence.Mappings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

internal sealed class MongoDbContext : IMongoDbContext
{
    private static readonly object ConfigurationLock = new();
    private static bool _isConfigured;

    public MongoDbContext(
        IMongoClient mongoClient,
        IOptions<MongoDbSettings> mongoDbSettings,
        IEnumerable<IMongoCollectionMapping> collectionMappings)
    {
        EnsureCollectionMappingsConfigured(collectionMappings);

        var database = mongoClient.GetDatabase(mongoDbSettings.Value.DatabaseName);
        ServiceOrderJobs = database.GetCollection<ServiceOrderJobDocument>(mongoDbSettings.Value.ServiceOrderJobsCollectionName);
    }

    public IMongoCollection<ServiceOrderJobDocument> ServiceOrderJobs { get; }

    private static void EnsureCollectionMappingsConfigured(IEnumerable<IMongoCollectionMapping> collectionMappings)
    {
        if (_isConfigured)
        {
            return;
        }

        lock (ConfigurationLock)
        {
            if (_isConfigured)
            {
                return;
            }

            foreach (var collectionMapping in collectionMappings)
            {
                collectionMapping.Configure();
            }

            _isConfigured = true;
        }
    }
}

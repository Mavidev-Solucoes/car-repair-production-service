using Infrastructure.Persistence.Documents;
using Infrastructure.Persistence.Mappings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

internal sealed class MongoDbContext : IMongoDbContext
{
    private static readonly object ConfigurationLock = new();
    private static bool _isConfigured;
    private static bool _areIndexesEnsured;

    public MongoDbContext(
        IMongoClient mongoClient,
        IOptions<MongoDbSettings> mongoDbSettings,
        IEnumerable<IMongoCollectionMapping> collectionMappings)
    {
        EnsureCollectionMappingsConfigured(collectionMappings);

        var settings = mongoDbSettings.Value;
        var database = mongoClient.GetDatabase(settings.DatabaseName);

        EnsureIndexesConfigured(database, settings, collectionMappings);
        ServiceOrderJobs = database.GetCollection<ServiceOrderJobDocument>(settings.ServiceOrderJobsCollectionName);
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

    private static void EnsureIndexesConfigured(
        IMongoDatabase database,
        MongoDbSettings settings,
        IEnumerable<IMongoCollectionMapping> collectionMappings)
    {
        if (_areIndexesEnsured)
        {
            return;
        }

        lock (ConfigurationLock)
        {
            if (_areIndexesEnsured)
            {
                return;
            }

            foreach (var collectionMapping in collectionMappings)
            {
                collectionMapping.EnsureIndexes(database, settings);
            }

            _areIndexesEnsured = true;
        }
    }
}

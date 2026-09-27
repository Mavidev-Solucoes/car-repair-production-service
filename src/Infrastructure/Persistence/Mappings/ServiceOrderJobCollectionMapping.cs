using Infrastructure.Persistence.Documents;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Mappings;

internal sealed class ServiceOrderJobCollectionMapping : IMongoCollectionMapping
{
    private const string CreatedAtDescendingIndexName = "ix_service_order_jobs_created_at_desc";

    public void Configure()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(ServiceOrderJobDocument)))
        {
            BsonClassMap.RegisterClassMap<ServiceOrderJobDocument>(classMap =>
            {
                classMap.AutoMap();
                classMap.MapIdMember(document => document.Id).SetSerializer(new GuidSerializer(MongoDB.Bson.GuidRepresentation.Standard));
            });
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(ServiceJobDocument)))
        {
            BsonClassMap.RegisterClassMap<ServiceJobDocument>(classMap => classMap.AutoMap());
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(ServiceOrderJobStatusHistoryDocument)))
        {
            BsonClassMap.RegisterClassMap<ServiceOrderJobStatusHistoryDocument>(classMap => classMap.AutoMap());
        }
    }

    public void EnsureIndexes(IMongoDatabase database, MongoDbSettings settings)
    {
        var collection = database.GetCollection<ServiceOrderJobDocument>(settings.ServiceOrderJobsCollectionName);
        var createdAtIndex = new CreateIndexModel<ServiceOrderJobDocument>(
            Builders<ServiceOrderJobDocument>.IndexKeys.Descending(document => document.CreatedAtUtc),
            new CreateIndexOptions
            {
                Name = CreatedAtDescendingIndexName
            });

        collection.Indexes.CreateOne(createdAtIndex);
    }
}

using Infrastructure.Persistence.Documents;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Infrastructure.Persistence.Mappings;

internal sealed class ServiceOrderJobCollectionMapping : IMongoCollectionMapping
{
    private const string CreatedAtDescendingIndexName = "ix_service_order_jobs_created_at_desc";
    private const string ServiceOrderIdIndexName = "ux_service_order_jobs_service_order_id";

    public void Configure()
    {
        // No modo V2 (padrão do driver 2.x) o class map grava UUID padrão (subtipo 4), mas os filtros saem no
        // formato legado do C# (subtipo 3) e a busca por id nunca encontra o documento. O modo V3 (padrão do
        // driver 3.x) respeita o serializer em leitura e escrita.
#pragma warning disable CS0618
        BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;
#pragma warning restore CS0618
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        if (!BsonClassMap.IsClassMapRegistered(typeof(ServiceOrderJobDocument)))
        {
            BsonClassMap.RegisterClassMap<ServiceOrderJobDocument>(classMap =>
            {
                classMap.AutoMap();
                classMap.MapIdMember(document => document.Id).SetSerializer(new GuidSerializer(MongoDB.Bson.GuidRepresentation.Standard));
                classMap.MapMember(document => document.ServiceOrderId).SetSerializer(new GuidSerializer(MongoDB.Bson.GuidRepresentation.Standard));
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

        var serviceOrderIdIndex = new CreateIndexModel<ServiceOrderJobDocument>(
            Builders<ServiceOrderJobDocument>.IndexKeys.Ascending(document => document.ServiceOrderId),
            new CreateIndexOptions
            {
                Name = ServiceOrderIdIndexName,
                Unique = true
            });

        collection.Indexes.CreateMany([createdAtIndex, serviceOrderIdIndex]);
    }

    public string GetIndexTargetKey(MongoDbSettings settings) => settings.ServiceOrderJobsCollectionName;
}

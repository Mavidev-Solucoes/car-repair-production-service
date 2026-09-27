using Infrastructure.Persistence.Documents;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Infrastructure.Persistence.Mappings;

internal sealed class ServiceOrderJobCollectionMapping : IMongoCollectionMapping
{
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
}

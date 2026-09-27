using MongoDB.Driver;

namespace Infrastructure.Persistence.Mappings;

internal interface IMongoCollectionMapping
{
    void Configure();

    void EnsureIndexes(IMongoDatabase database, MongoDbSettings settings);

    string GetIndexTargetKey(MongoDbSettings settings);
}

using Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Infrastructure.Persistence;

internal interface IMongoDbContext
{
    IMongoCollection<ServiceOrderJobDocument> ServiceOrderJobs { get; }
}

namespace Infrastructure.Persistence;

public sealed class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;

    public string ServiceOrderJobsCollectionName { get; init; } = "serviceOrderJobs";
}

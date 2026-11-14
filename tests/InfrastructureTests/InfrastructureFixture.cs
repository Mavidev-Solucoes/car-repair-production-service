using Application;
using Infrastructure;
using Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Testcontainers.MongoDb;
using Testcontainers.RabbitMq;
using Xunit;

namespace InfrastructureTests;

/// <summary>
/// Sobe um Mongo e um RabbitMQ reais (Testcontainers) uma vez para toda a coleção de testes.
/// </summary>
public sealed class InfrastructureFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithImage("mongo:7.0").Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    public string MongoConnectionString => _mongo.GetConnectionString();

    public string RabbitHost => _rabbit.Hostname;

    public int RabbitPort => _rabbit.GetMappedPublicPort(5672);

    public Task InitializeAsync() => Task.WhenAll(_mongo.StartAsync(), _rabbit.StartAsync());

    public async Task DisposeAsync()
    {
        await _mongo.DisposeAsync();
        await _rabbit.DisposeAsync();
    }

    public RabbitMqSettings RabbitSettings(bool enabled = true, int retryCount = 3, int retryDelayMilliseconds = 200) => new()
    {
        Enabled = enabled,
        HostName = RabbitHost,
        Port = RabbitPort,
        RetryCount = retryCount,
        RetryDelayMilliseconds = retryDelayMilliseconds,
        Durable = false
    };

    public RabbitMqCommandConsumer CreateConsumer(RabbitMqSettings settings)
        => new(Options.Create(settings), NullLogger<RabbitMqCommandConsumer>.Instance);

    public RabbitMqEventPublisher CreatePublisher(RabbitMqSettings settings)
        => new(Options.Create(settings), NullLogger<RabbitMqEventPublisher>.Instance);

    public IConnection CreateRawConnection()
        => new ConnectionFactory { HostName = RabbitHost, Port = RabbitPort, UserName = "guest", Password = "guest" }.CreateConnection();

    public IConfiguration BuildConfiguration(string databaseName, bool rabbitEnabled = false) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MongoDb:ConnectionString"] = MongoConnectionString,
            ["MongoDb:DatabaseName"] = databaseName,
            ["RabbitMq:Enabled"] = rabbitEnabled.ToString(),
            ["RabbitMq:HostName"] = RabbitHost,
            ["RabbitMq:Port"] = RabbitPort.ToString(),
            ["RabbitMq:RetryCount"] = "1",
            ["RabbitMq:RetryDelayMilliseconds"] = "200",
            ["RabbitMq:Durable"] = "false"
        })
        .Build();

    public ServiceProvider BuildServices(string databaseName, bool rabbitEnabled = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(BuildConfiguration(databaseName, rabbitEnabled));
        return services.BuildServiceProvider();
    }

    public static async Task<T> WaitForAsync<T>(Func<Task<T?>> probe, TimeSpan? timeout = null)
        where T : class
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (DateTime.UtcNow < deadline)
        {
            var value = await probe();
            if (value is not null)
            {
                return value;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Condição não foi atingida a tempo.");
    }
}

[CollectionDefinition(Name)]
public sealed class InfrastructureCollection : ICollectionFixture<InfrastructureFixture>
{
    public const string Name = "infrastructure";
}

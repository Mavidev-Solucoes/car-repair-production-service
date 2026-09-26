using Application.Common.Interfaces;
using Application.Common.Messaging;
using Infrastructure.Messaging;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Mappings;
using Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<MongoDbSettings>()
            .Bind(configuration.GetSection(MongoDbSettings.SectionName))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.ConnectionString), "MongoDb:ConnectionString is required.")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.DatabaseName), "MongoDb:DatabaseName is required.")
            .ValidateOnStart();

        services
            .AddOptions<RabbitMqSettings>()
            .Bind(configuration.GetSection(RabbitMqSettings.SectionName))
            .Validate(settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.HostName), "RabbitMq:HostName is required when enabled.")
            .Validate(settings => !settings.Enabled || settings.Port > 0, "RabbitMq:Port must be greater than zero when enabled.")
            .Validate(settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.ExchangeName), "RabbitMq:ExchangeName is required when enabled.")
            .Validate(settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.DeadLetterExchangeName), "RabbitMq:DeadLetterExchangeName is required when enabled.")
            .Validate(settings => !settings.Enabled || settings.RetryCount >= 0, "RabbitMq:RetryCount must be zero or greater when enabled.")
            .Validate(settings => !settings.Enabled || settings.RetryDelayMilliseconds > 0, "RabbitMq:RetryDelayMilliseconds must be greater than zero when enabled.")
            .ValidateOnStart();

        services.AddSingleton<IMongoClient>(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            return new MongoClient(settings.ConnectionString);
        });

        services.AddSingleton<IMongoCollectionMapping, ServiceOrderJobCollectionMapping>();
        services.AddScoped<IMongoDbContext, MongoDbContext>();
        services.AddScoped<IServiceOrderJobRepository, ServiceOrderJobRepository>();

        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        services.AddSingleton<ICommandConsumer, RabbitMqCommandConsumer>();

        return services;
    }
}

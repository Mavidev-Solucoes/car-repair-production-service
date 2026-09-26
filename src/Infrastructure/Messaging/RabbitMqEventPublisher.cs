using System.Text.Json;
using Application.Common.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public sealed class RabbitMqEventPublisher(
    IOptions<RabbitMqSettings> options,
    ILogger<RabbitMqEventPublisher> logger)
    : IEventPublisher
{
    private readonly RabbitMqSettings _settings = options.Value;

    public Task PublishAsync<TEvent>(MessageEnvelope<TEvent> envelope, string routingKey, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_settings.Enabled)
        {
            logger.LogDebug("RabbitMq publishing is disabled. Event {EventType} was not published.", envelope.EventType);
            return Task.CompletedTask;
        }

        var factory = BuildConnectionFactory();

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(_settings.ExchangeName, ExchangeType.Topic, _settings.Durable, false);
        channel.ExchangeDeclare(_settings.DeadLetterExchangeName, ExchangeType.Topic, _settings.Durable, false);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.MessageId = envelope.MessageId.ToString();
        properties.CorrelationId = envelope.CorrelationId;
        properties.Type = envelope.EventType;
        properties.Timestamp = new AmqpTimestamp(new DateTimeOffset(envelope.OccurredAt).ToUnixTimeSeconds());
        properties.Headers = new Dictionary<string, object?>
        {
            ["event-version"] = envelope.EventVersion,
            ["saga-id"] = envelope.SagaId ?? string.Empty
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(envelope);

        channel.BasicPublish(
            exchange: _settings.ExchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);

        logger.LogInformation(
            "Published event {EventType} to exchange {Exchange} with routing key {RoutingKey}",
            envelope.EventType,
            _settings.ExchangeName,
            routingKey);

        return Task.CompletedTask;
    }

    private ConnectionFactory BuildConnectionFactory()
    {
        return new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password,
            VirtualHost = _settings.VirtualHost,
            DispatchConsumersAsync = true
        };
    }
}

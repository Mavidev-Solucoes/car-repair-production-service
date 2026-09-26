using System.Text.Json;
using Application.Common.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public sealed class RabbitMqEventPublisher(
    IOptions<RabbitMqSettings> options,
    ILogger<RabbitMqEventPublisher> logger)
    : IEventPublisher, IDisposable
{
    private readonly RabbitMqSettings _settings = options.Value;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private IConnection? _connection;
    private IModel? _channel;

    public async Task PublishAsync<TEvent>(MessageEnvelope<TEvent> envelope, string routingKey, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_settings.Enabled)
        {
            logger.LogDebug("RabbitMq publishing is disabled. Event {EventType} was not published.", envelope.EventType);
            return;
        }

        await _channelLock.WaitAsync(cancellationToken);
        try
        {
            EnsureChannel();

            var channel = _channel ?? throw new InvalidOperationException("RabbitMQ channel is not initialized.");

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
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_channel is not null)
            {
                if (_channel.IsOpen)
                {
                    _channel.Close();
                }

                _channel.Dispose();
                _channel = null;
            }

            if (_connection is not null)
            {
                if (_connection.IsOpen)
                {
                    _connection.Close();
                }

                _connection.Dispose();
                _connection = null;
            }
        }

        _channelLock.Dispose();
    }

    private void EnsureChannel()
    {
        lock (_sync)
        {
            if (_connection is null || !_connection.IsOpen)
            {
                _connection?.Dispose();
                _connection = BuildConnectionFactory().CreateConnection();
            }

            if (_channel is null || !_channel.IsOpen)
            {
                _channel?.Dispose();
                _channel = _connection.CreateModel();
            }
        }
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

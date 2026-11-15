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

    public async Task PublishAsync(IntegrationMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_settings.Enabled)
        {
            logger.LogDebug("RabbitMq publishing is disabled. Event {EventType} was not published.", message.Type);
            return;
        }

        await _channelLock.WaitAsync(cancellationToken);
        try
        {
            EnsureChannel();

            var channel = _channel ?? throw new InvalidOperationException("RabbitMQ channel is not initialized.");

            channel.ExchangeDeclare(_settings.ExchangeName, ExchangeType.Topic, _settings.Durable, false);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.MessageId = message.MessageId.ToString();
            properties.CorrelationId = message.CorrelationId;
            properties.Type = message.Type;
            properties.Timestamp = new AmqpTimestamp(new DateTimeOffset(DateTime.SpecifyKind(message.OccurredAtUtc, DateTimeKind.Utc)).ToUnixTimeSeconds());
            properties.Headers ??= new Dictionary<string, object>();
            MessageTracing.Inject(properties.Headers);

            var body = JsonSerializer.SerializeToUtf8Bytes(message, IntegrationMessage.SerializerOptions);

            channel.BasicPublish(
                exchange: _settings.ExchangeName,
                routingKey: message.Type,
                mandatory: false,
                basicProperties: properties,
                body: body);

            logger.LogInformation(
                "Published event {EventType} to exchange {Exchange} with routing key {RoutingKey}",
                message.Type,
                _settings.ExchangeName,
                message.Type);
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

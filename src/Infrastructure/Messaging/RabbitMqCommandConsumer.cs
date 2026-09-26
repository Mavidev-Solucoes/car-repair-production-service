using System.Text.Json;
using Application.Common.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Infrastructure.Messaging;

public sealed class RabbitMqCommandConsumer(
    IOptions<RabbitMqSettings> options,
    ILogger<RabbitMqCommandConsumer> logger)
    : ICommandConsumer, IDisposable
{
    private readonly RabbitMqSettings _settings = options.Value;
    private readonly List<IConnection> _connections = [];
    private readonly List<IModel> _channels = [];
    private readonly object _sync = new();

    public Task SubscribeAsync<TCommand>(
        string queueName,
        string routingKey,
        Func<MessageEnvelope<TCommand>, CancellationToken, Task> onMessage,
        CancellationToken cancellationToken = default)
        where TCommand : class
    {
        if (!_settings.Enabled)
        {
            logger.LogDebug("RabbitMq consumption is disabled. Queue {QueueName} was not subscribed.", queueName);
            return Task.CompletedTask;
        }

        var factory = BuildConnectionFactory();
        var connection = factory.CreateConnection();
        var channel = connection.CreateModel();

        var retryExchangeName = $"{_settings.ExchangeName}.retry";
        var retryQueueName = $"{queueName}.retry";
        var deadLetterQueueName = $"{queueName}.dlq";

        channel.ExchangeDeclare(_settings.ExchangeName, ExchangeType.Topic, _settings.Durable, false);
        channel.ExchangeDeclare(_settings.DeadLetterExchangeName, ExchangeType.Topic, _settings.Durable, false);
        channel.ExchangeDeclare(retryExchangeName, ExchangeType.Topic, _settings.Durable, false);

        channel.QueueDeclare(
            queue: queueName,
            durable: _settings.Durable,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = _settings.DeadLetterExchangeName,
                ["x-dead-letter-routing-key"] = routingKey
            });

        channel.QueueBind(queueName, _settings.ExchangeName, routingKey);

        channel.QueueDeclare(
            queue: retryQueueName,
            durable: _settings.Durable,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-message-ttl"] = _settings.RetryDelayMilliseconds,
                ["x-dead-letter-exchange"] = _settings.ExchangeName,
                ["x-dead-letter-routing-key"] = routingKey
            });

        channel.QueueBind(retryQueueName, retryExchangeName, routingKey);

        channel.QueueDeclare(deadLetterQueueName, _settings.Durable, false, false, null);
        channel.QueueBind(deadLetterQueueName, _settings.DeadLetterExchangeName, routingKey);

        channel.BasicQos(0, _settings.PrefetchCount, false);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.Received += async (_, eventArgs) =>
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<MessageEnvelope<TCommand>>(eventArgs.Body.Span);
                if (envelope is null)
                {
                    throw new InvalidOperationException("Message envelope could not be deserialized.");
                }

                await onMessage(envelope, cancellationToken);
                channel.BasicAck(eventArgs.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                var retryCount = GetRetryCount(eventArgs.BasicProperties?.Headers);

                if (retryCount < _settings.RetryCount)
                {
                    var retryProperties = BuildProperties(channel, eventArgs.BasicProperties, retryCount + 1);
                    channel.BasicPublish(retryExchangeName, routingKey, false, retryProperties, eventArgs.Body);

                    logger.LogWarning(
                        ex,
                        "Message processing failed for queue {QueueName}. Retrying attempt {RetryAttempt}/{RetryCount}.",
                        queueName,
                        retryCount + 1,
                        _settings.RetryCount);
                }
                else
                {
                    var deadLetterProperties = BuildProperties(channel, eventArgs.BasicProperties, retryCount);
                    channel.BasicPublish(_settings.DeadLetterExchangeName, routingKey, false, deadLetterProperties, eventArgs.Body);

                    logger.LogError(
                        ex,
                        "Message processing failed for queue {QueueName}. Sent to dead-letter queue after {RetryCount} retries.",
                        queueName,
                        _settings.RetryCount);
                }

                channel.BasicAck(eventArgs.DeliveryTag, false);
            }
        };

        var consumerTag = channel.BasicConsume(queueName, false, consumer);
        cancellationToken.Register(() =>
        {
            if (channel.IsOpen)
            {
                channel.BasicCancel(consumerTag);
            }
        });

        lock (_sync)
        {
            _connections.Add(connection);
            _channels.Add(channel);
        }

        logger.LogInformation(
            "Subscribed queue {QueueName} to exchange {ExchangeName} with routing key {RoutingKey}",
            queueName,
            _settings.ExchangeName,
            routingKey);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            foreach (var channel in _channels)
            {
                if (channel.IsOpen)
                {
                    channel.Close();
                }

                channel.Dispose();
            }

            foreach (var connection in _connections)
            {
                if (connection.IsOpen)
                {
                    connection.Close();
                }

                connection.Dispose();
            }

            _channels.Clear();
            _connections.Clear();
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

    private static int GetRetryCount(IDictionary<string, object>? headers)
    {
        if (headers is null || !headers.TryGetValue("x-retry-count", out var value) || value is null)
        {
            return 0;
        }

        return value switch
        {
            byte[] bytes when int.TryParse(System.Text.Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
            int parsed => parsed,
            long parsed => (int)parsed,
            _ => 0
        };
    }

    private static IBasicProperties BuildProperties(IModel channel, IBasicProperties? source, int retryCount)
    {
        var properties = channel.CreateBasicProperties();
        properties.Persistent = source?.Persistent ?? true;
        properties.ContentType = source?.ContentType ?? "application/json";
        properties.CorrelationId = source?.CorrelationId;
        properties.MessageId = source?.MessageId;
        properties.Type = source?.Type;
        properties.Headers = source?.Headers is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(source.Headers);
        properties.Headers["x-retry-count"] = retryCount;

        return properties;
    }
}

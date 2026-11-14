using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Common.Messaging;
using Infrastructure.Messaging;
using RabbitMQ.Client;
using Xunit;

namespace InfrastructureTests;

[Collection(InfrastructureCollection.Name)]
public sealed class RabbitMqMessagingTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Publisher_ShouldPublishEnvelopeWithAmqpProperties()
    {
        var settings = fixture.RabbitSettings();
        var eventType = UniqueName("WorkCompleted");
        using var connection = fixture.CreateRawConnection();
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare(settings.ExchangeName, ExchangeType.Topic, settings.Durable, false);
        var queue = channel.QueueDeclare().QueueName;
        channel.QueueBind(queue, settings.ExchangeName, eventType);

        using var publisher = fixture.CreatePublisher(settings);
        var message = new IntegrationMessage
        {
            Type = eventType,
            CorrelationId = Guid.NewGuid().ToString("D"),
            Payload = new JsonObject { ["workOrderId"] = Guid.NewGuid() }
        };

        await publisher.PublishAsync(message);
        await publisher.PublishAsync(message);

        var delivery = await InfrastructureFixture.WaitForAsync(() => Task.FromResult(channel.BasicGet(queue, true)));
        var received = JsonSerializer.Deserialize<IntegrationMessage>(delivery.Body.Span, IntegrationMessage.SerializerOptions)!;

        Assert.Equal(message.MessageId.ToString(), delivery.BasicProperties.MessageId);
        Assert.Equal(message.CorrelationId, delivery.BasicProperties.CorrelationId);
        Assert.Equal(eventType, delivery.BasicProperties.Type);
        Assert.Equal("application/json", delivery.BasicProperties.ContentType);
        Assert.Equal(message.CorrelationId, received.CorrelationId);
        Assert.Equal(message.Payload["workOrderId"]!.GetValue<Guid>(), received.Payload!["workOrderId"]!.GetValue<Guid>());
    }

    [Fact]
    public async Task Publisher_WhenDisabled_ShouldDoNothing()
    {
        using var publisher = fixture.CreatePublisher(fixture.RabbitSettings(enabled: false));

        await publisher.PublishAsync(new IntegrationMessage { Type = "Ignored", CorrelationId = Guid.NewGuid().ToString() });
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            publisher.PublishAsync(new IntegrationMessage { Type = "Ignored" }, new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task Consumer_ShouldDeliverAndAck()
    {
        var settings = fixture.RabbitSettings();
        var commandType = UniqueName("CreateWorkOrderCommand");
        var received = new TaskCompletionSource<IntegrationMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var consumer = fixture.CreateConsumer(settings);

        await consumer.SubscribeAsync($"production-tests.{commandType}", commandType, (message, _) =>
        {
            received.TrySetResult(message);
            return Task.CompletedTask;
        });

        var correlationId = Guid.NewGuid().ToString("D");
        PublishCommand(settings, commandType, Envelope(commandType, correlationId));

        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(correlationId, message.CorrelationId);
        await AssertQueueEmptyEventuallyAsync($"production-tests.{commandType}");
    }

    [Fact]
    public async Task Consumer_ShouldRetryTransientFailures()
    {
        var settings = fixture.RabbitSettings(retryCount: 3, retryDelayMilliseconds: 100);
        var commandType = UniqueName("RetryCommand");
        var attempts = 0;
        var succeeded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var consumer = fixture.CreateConsumer(settings);

        await consumer.SubscribeAsync($"production-tests.{commandType}", commandType, (_, _) =>
        {
            if (Interlocked.Increment(ref attempts) < 3)
            {
                throw new InvalidOperationException("Mongo fora do ar");
            }

            succeeded.TrySetResult();
            return Task.CompletedTask;
        });

        PublishCommand(settings, commandType, Envelope(commandType, Guid.NewGuid().ToString()));

        await succeeded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task Consumer_ShouldSendToDlqAfterExhaustingRetries()
    {
        var settings = fixture.RabbitSettings(retryCount: 1, retryDelayMilliseconds: 100);
        var commandType = UniqueName("AlwaysFailingCommand");
        var attempts = 0;
        using var consumer = fixture.CreateConsumer(settings);

        await consumer.SubscribeAsync($"production-tests.{commandType}", commandType, (_, _) =>
        {
            Interlocked.Increment(ref attempts);
            throw new InvalidOperationException("falha permanente");
        });

        PublishCommand(settings, commandType, Envelope(commandType, Guid.NewGuid().ToString()));

        var dead = await GetFromDlqAsync($"production-tests.{commandType}");
        Assert.Equal(2, attempts);
        Assert.Equal(1, Convert.ToInt32(dead.BasicProperties.Headers["x-retry-count"]));
    }

    [Fact]
    public async Task Consumer_ShouldSendInvalidMessagesStraightToDlq()
    {
        var settings = fixture.RabbitSettings(retryCount: 3);
        var commandType = UniqueName("InvalidCommand");
        var attempts = 0;
        using var consumer = fixture.CreateConsumer(settings);

        await consumer.SubscribeAsync($"production-tests.{commandType}", commandType, (_, _) =>
        {
            Interlocked.Increment(ref attempts);
            throw new InvalidMessageException("payload sem itens");
        });

        PublishCommand(settings, commandType, Envelope(commandType, Guid.NewGuid().ToString()));
        PublishCommand(settings, commandType, Encoding.UTF8.GetBytes("isto não é json"));
        PublishCommand(settings, commandType, Encoding.UTF8.GetBytes("{\"type\":\"\",\"correlationId\":\"\"}"));

        var queue = $"production-tests.{commandType}";
        await GetFromDlqAsync(queue);
        await GetFromDlqAsync(queue);
        await GetFromDlqAsync(queue);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Consumer_WhenDisabled_ShouldNotDeclareAnything()
    {
        var consumer = fixture.CreateConsumer(fixture.RabbitSettings(enabled: false));
        var commandType = UniqueName("DisabledCommand");

        await consumer.SubscribeAsync($"production-tests.{commandType}", commandType, (_, _) => Task.CompletedTask);
        consumer.Dispose();

        using var connection = fixture.CreateRawConnection();
        using var channel = connection.CreateModel();
        Assert.ThrowsAny<Exception>(() => channel.QueueDeclarePassive($"production-tests.{commandType}"));
    }

    [Fact]
    public async Task Consumer_ShouldStopConsumingWhenCancelled()
    {
        var settings = fixture.RabbitSettings();
        var commandType = UniqueName("CancelledCommand");
        var queue = $"production-tests.{commandType}";
        using var cancellation = new CancellationTokenSource();
        var consumer = fixture.CreateConsumer(settings);

        await consumer.SubscribeAsync(queue, commandType, (_, _) => Task.CompletedTask, cancellation.Token);
        cancellation.Cancel();

        using var connection = fixture.CreateRawConnection();
        using var channel = connection.CreateModel();
        await InfrastructureFixture.WaitForAsync(() =>
            Task.FromResult(channel.QueueDeclarePassive(queue).ConsumerCount == 0 ? "ok" : null));

        consumer.Dispose();
        consumer.Dispose();
    }

    [Fact]
    public void Deserialize_ShouldRejectBrokenEnvelopes()
    {
        Assert.Throws<InvalidMessageException>(() => RabbitMqCommandConsumer.Deserialize(Encoding.UTF8.GetBytes("null")));
        Assert.Throws<InvalidMessageException>(() => RabbitMqCommandConsumer.Deserialize(Encoding.UTF8.GetBytes("{\"type\":\"X\"}")));

        var ok = RabbitMqCommandConsumer.Deserialize(Envelope("X", "abc"));
        Assert.Equal("X", ok.Type);
    }

    private void PublishCommand(RabbitMqSettings settings, string routingKey, byte[] body)
    {
        using var connection = fixture.CreateRawConnection();
        using var channel = connection.CreateModel();
        var properties = channel.CreateBasicProperties();
        properties.ContentType = "application/json";
        channel.BasicPublish(settings.CommandsExchangeName, routingKey, false, properties, body);
    }

    private async Task<BasicGetResult> GetFromDlqAsync(string queue)
    {
        using var connection = fixture.CreateRawConnection();
        using var channel = connection.CreateModel();
        return await InfrastructureFixture.WaitForAsync(() => Task.FromResult(channel.BasicGet($"{queue}.dlq", true)));
    }

    private async Task AssertQueueEmptyEventuallyAsync(string queue)
    {
        using var connection = fixture.CreateRawConnection();
        using var channel = connection.CreateModel();
        await InfrastructureFixture.WaitForAsync(() =>
            Task.FromResult(channel.QueueDeclarePassive(queue).MessageCount == 0 ? "ok" : null));
    }

    private static byte[] Envelope(string type, string correlationId)
        => JsonSerializer.SerializeToUtf8Bytes(
            new IntegrationMessage { Type = type, CorrelationId = correlationId, Payload = new JsonObject() },
            IntegrationMessage.SerializerOptions);

    private static string UniqueName(string prefix) => $"{prefix}.{Guid.NewGuid():N}";
}

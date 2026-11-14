using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Common.Interfaces;
using Application.Common.Messaging;
using Application.WorkOrders.Commands.CreateWorkOrder;
using Domain.Entities;
using Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Xunit;

namespace InfrastructureTests;

[Collection(InfrastructureCollection.Name)]
public sealed class SagaCommandsWorkerTests(InfrastructureFixture fixture)
{
    private const string Queue = SagaCommandsWorker.QueuePrefix + CreateWorkOrderSagaCommandMapper.CommandType;

    [Fact]
    public async Task Worker_ShouldCreateWorkOrderFromSagaCommandAndPublishEvent()
    {
        await using var provider = fixture.BuildServices($"production_worker_{Guid.NewGuid():N}", rabbitEnabled: true);
        var worker = provider.GetServices<IHostedService>().OfType<SagaCommandsWorker>().Single();
        using var connection = fixture.CreateRawConnection();
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare("car-repair.events", ExchangeType.Topic, false, false);
        var eventsQueue = channel.QueueDeclare().QueueName;
        channel.QueueBind(eventsQueue, "car-repair.events", "WorkOrderCreated");

        await worker.StartAsync(CancellationToken.None);
        try
        {
            var serviceOrderId = Guid.NewGuid();
            var payload = new JsonObject
            {
                ["serviceOrderId"] = serviceOrderId,
                ["items"] = new JsonArray(new JsonObject { ["description"] = "Troca de óleo", ["quantity"] = 1 })
            };

            await WaitForQueueAsync(channel);
            Publish(channel, serviceOrderId.ToString("D"), payload);
            Publish(channel, serviceOrderId.ToString("D"), payload);

            var workOrder = await InfrastructureFixture.WaitForAsync(async () =>
            {
                using var scope = provider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IServiceOrderJobRepository>();
                return await repository.GetByServiceOrderIdAsync(serviceOrderId, CancellationToken.None);
            });

            Assert.Equal("1x Troca de óleo", workOrder.ServiceJob.Description);
            var created = await InfrastructureFixture.WaitForAsync(() => Task.FromResult(channel.BasicGet(eventsQueue, true)));
            Assert.Equal(serviceOrderId.ToString("D"), created.BasicProperties.CorrelationId);

            await Task.Delay(500);
            using var check = provider.CreateScope();
            var all = await check.ServiceProvider.GetRequiredService<IServiceOrderJobRepository>().ListAsync(CancellationToken.None);
            Assert.Single(all, job => job.ServiceOrderId == serviceOrderId);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task HandleCreateWorkOrder_WithInvalidCorrelation_ShouldBeInvalidMessage()
    {
        await using var provider = fixture.BuildServices($"production_worker_{Guid.NewGuid():N}");
        var worker = provider.GetServices<IHostedService>().OfType<SagaCommandsWorker>().Single();

        await Assert.ThrowsAsync<InvalidMessageException>(() => worker.HandleCreateWorkOrderAsync(
            new IntegrationMessage { Type = CreateWorkOrderSagaCommandMapper.CommandType, CorrelationId = "nao-e-guid" },
            CancellationToken.None));
    }

    [Fact]
    public async Task Worker_ShouldKeepRetryingSubscriptionUntilStopped()
    {
        var consumer = new FailingConsumer();
        await using var provider = fixture.BuildServices($"production_worker_{Guid.NewGuid():N}");
        using var worker = new SagaCommandsWorker(
            consumer,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SagaCommandsWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await InfrastructureFixture.WaitForAsync(() => Task.FromResult(consumer.Attempts > 0 ? "ok" : null));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(consumer.Attempts >= 1);
    }

    private static async Task WaitForQueueAsync(IModel channel)
    {
        await InfrastructureFixture.WaitForAsync(() =>
        {
            try
            {
                return Task.FromResult(channel.QueueDeclarePassive(Queue).ConsumerCount > 0 ? "ok" : null);
            }
            catch
            {
                return Task.FromResult<string?>(null);
            }
        });
    }

    private static void Publish(IModel channel, string correlationId, JsonObject payload)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(
            new IntegrationMessage
            {
                Type = CreateWorkOrderSagaCommandMapper.CommandType,
                CorrelationId = correlationId,
                Payload = payload
            },
            IntegrationMessage.SerializerOptions);
        channel.BasicPublish("car-repair.commands", CreateWorkOrderSagaCommandMapper.CommandType, false, null, body);
    }

    private sealed class FailingConsumer : ICommandConsumer
    {
        public int Attempts;

        public Task SubscribeAsync(
            string queueName,
            string commandType,
            Func<IntegrationMessage, CancellationToken, Task> onMessage,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Attempts);
            throw new InvalidOperationException("RabbitMQ indisponível");
        }
    }
}

using System.Text.Json.Nodes;
using Application.Common.Interfaces;
using Application.Common.Messaging;
using Application.WorkOrders.Commands.CreateWorkOrder;
using Domain.Entities;
using Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace UnitTests;

public sealed class SagaContractTests
{
    private static readonly Guid ServiceOrderId = Guid.Parse("3d9c4f8a-1b2e-4c6d-8e7f-001122334455");

    [Fact]
    public void Map_ShouldBuildWorkOrderFromSagaContext()
    {
        var message = new IntegrationMessage
        {
            Type = CreateWorkOrderSagaCommandMapper.CommandType,
            CorrelationId = ServiceOrderId.ToString(),
            Payload = new JsonObject
            {
                ["customerName"] = "Maria",
                ["items"] = new JsonArray(
                    new JsonObject { ["description"] = "Troca de óleo", ["quantity"] = 1 },
                    new JsonObject { ["description"] = " Filtro de ar ", ["quantity"] = 2 },
                    new JsonObject { ["description"] = "" })
            }
        };

        var command = CreateWorkOrderSagaCommandMapper.Map(message);

        Assert.Equal(ServiceOrderId, command.ServiceOrderId);
        Assert.Equal(ServiceOrderId, command.ServiceJobId);
        Assert.Equal("OS 3D9C4F8A", command.ServiceJobName);
        Assert.Equal("1x Troca de óleo; 2x Filtro de ar", command.ServiceJobDescription);
    }

    [Fact]
    public void Map_ShouldAcceptPayloadWithoutItems()
    {
        var message = new IntegrationMessage { Type = "CreateWorkOrderCommand", CorrelationId = ServiceOrderId.ToString() };

        var command = CreateWorkOrderSagaCommandMapper.Map(message);

        Assert.Null(command.ServiceJobDescription);
    }

    [Fact]
    public void Map_ShouldTruncateLongDescription()
    {
        var items = new JsonArray();
        for (var i = 0; i < 40; i++)
        {
            items.Add(new JsonObject { ["description"] = $"Serviço número {i} com descrição comprida", ["quantity"] = 1 });
        }

        var command = CreateWorkOrderSagaCommandMapper.Map(new IntegrationMessage
        {
            Type = "CreateWorkOrderCommand",
            CorrelationId = ServiceOrderId.ToString(),
            Payload = new JsonObject { ["items"] = items }
        });

        Assert.Equal(ServiceJob.DescriptionMaxLength, command.ServiceJobDescription!.Length);
        Assert.EndsWith("...", command.ServiceJobDescription);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void GetServiceOrderId_ShouldRejectInvalidCorrelationId(string correlationId)
    {
        var message = new IntegrationMessage { Type = "CreateWorkOrderCommand", CorrelationId = correlationId };

        Assert.Throws<InvalidMessageException>(() => message.GetServiceOrderId());
    }

    [Fact]
    public void ToIntegrationMessage_ShouldFollowContract()
    {
        var workOrderId = Guid.NewGuid();
        var domainEvent = new WorkFailedDomainEvent(workOrderId, "Peça indisponível");

        var message = DomainEventPublisherExtensions.ToIntegrationMessage(domainEvent, ServiceOrderId);

        Assert.Equal("WorkFailed", message.Type);
        Assert.Equal(ServiceOrderId.ToString("D"), message.CorrelationId);
        Assert.Equal(domainEvent.OccurredOnUtc, message.OccurredAtUtc);
        Assert.Equal(workOrderId, message.Payload!["workOrderId"]!.GetValue<Guid>());
        Assert.Equal("Peça indisponível", message.Payload["reason"]!.GetValue<string>());
        Assert.False(message.Payload.ContainsKey("occurredOnUtc"));
    }

    [Fact]
    public async Task PublishDomainEventsAsync_ShouldPublishEveryEventWithServiceOrderCorrelation()
    {
        var publisher = new RecordingEventPublisher();
        var workOrderId = Guid.NewGuid();

        await publisher.PublishDomainEventsAsync(
            [new WorkStartedDomainEvent(workOrderId), new WorkCompletedDomainEvent(workOrderId)],
            ServiceOrderId);

        Assert.Equal(["WorkStarted", "WorkCompleted"], publisher.Messages.Select(message => message.Type).ToArray());
        Assert.All(publisher.Messages, message => Assert.Equal(ServiceOrderId.ToString("D"), message.CorrelationId));
    }

    [Fact]
    public async Task CreateWorkOrder_ShouldBeIdempotentPerServiceOrder()
    {
        var repository = new InMemoryRepository();
        var publisher = new RecordingEventPublisher();
        var handler = new CreateWorkOrderCommandHandler(repository, publisher, NullLogger<CreateWorkOrderCommandHandler>.Instance);
        var command = new CreateWorkOrderCommand(ServiceOrderId, ServiceOrderId, "OS 3D9C4F8A", "1x Troca de óleo");

        var firstId = await handler.Handle(command, CancellationToken.None);
        var secondId = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(firstId, secondId);
        Assert.Single(repository.Items);
        Assert.Single(publisher.Messages);
        Assert.Equal("WorkOrderCreated", publisher.Messages[0].Type);
    }

    [Fact]
    public async Task CreateWorkOrder_ShouldNotFailWhenPublishingFails()
    {
        var repository = new InMemoryRepository();
        var handler = new CreateWorkOrderCommandHandler(repository, new FailingEventPublisher(), NullLogger<CreateWorkOrderCommandHandler>.Instance);

        var id = await handler.Handle(new CreateWorkOrderCommand(ServiceOrderId, ServiceOrderId, "OS", null), CancellationToken.None);

        Assert.Equal(id, Assert.Single(repository.Items).Id);
    }

    [Fact]
    public void Create_ShouldRequireServiceOrderId()
    {
        Assert.Throws<ArgumentException>(() =>
            ServiceOrderJob.Create(Guid.NewGuid(), Guid.Empty, new ServiceJob(Guid.NewGuid(), "OS", null)));
    }

    private sealed class RecordingEventPublisher : IEventPublisher
    {
        public List<IntegrationMessage> Messages { get; } = [];

        public Task PublishAsync(IntegrationMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingEventPublisher : IEventPublisher
    {
        public Task PublishAsync(IntegrationMessage message, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("RabbitMQ fora do ar");
    }

    private sealed class InMemoryRepository : IServiceOrderJobRepository
    {
        public List<ServiceOrderJob> Items { get; } = [];

        public Task AddAsync(ServiceOrderJob serviceOrderJob, CancellationToken cancellationToken)
        {
            Items.Add(serviceOrderJob);
            return Task.CompletedTask;
        }

        public Task<ServiceOrderJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

        public Task<ServiceOrderJob?> GetByServiceOrderIdAsync(Guid serviceOrderId, CancellationToken cancellationToken)
            => Task.FromResult(Items.FirstOrDefault(item => item.ServiceOrderId == serviceOrderId));

        public Task<IReadOnlyCollection<ServiceOrderJob>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ServiceOrderJob>>(Items);

        public Task UpdateAsync(ServiceOrderJob serviceOrderJob, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

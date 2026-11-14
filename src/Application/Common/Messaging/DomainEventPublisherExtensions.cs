using System.Text.Json;
using System.Text.Json.Nodes;
using Domain.Common;

namespace Application.Common.Messaging;

public static class DomainEventPublisherExtensions
{
    private const string DomainEventSuffix = "DomainEvent";

    /// <summary>
    /// Publica os eventos de domínio no envelope do contrato. O tipo é o nome da classe sem o sufixo
    /// "DomainEvent" (WorkCompletedDomainEvent → WorkCompleted) e o correlationId é o id da OS.
    /// </summary>
    public static async Task PublishDomainEventsAsync(
        this IEventPublisher eventPublisher,
        IEnumerable<IDomainEvent> domainEvents,
        Guid serviceOrderId,
        CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await eventPublisher.PublishAsync(ToIntegrationMessage(domainEvent, serviceOrderId), cancellationToken);
        }
    }

    public static IntegrationMessage ToIntegrationMessage(IDomainEvent domainEvent, Guid serviceOrderId)
    {
        var payload = JsonSerializer.SerializeToNode(domainEvent, domainEvent.GetType(), IntegrationMessage.SerializerOptions) as JsonObject ?? [];
        payload.Remove("occurredOnUtc");

        return new IntegrationMessage
        {
            Type = GetEventType(domainEvent),
            CorrelationId = serviceOrderId.ToString("D"),
            OccurredAtUtc = domainEvent.OccurredOnUtc,
            Payload = payload
        };
    }

    public static string GetEventType(IDomainEvent domainEvent)
    {
        var name = domainEvent.GetType().Name;
        return name.EndsWith(DomainEventSuffix, StringComparison.Ordinal) && name.Length > DomainEventSuffix.Length
            ? name[..^DomainEventSuffix.Length]
            : name;
    }
}

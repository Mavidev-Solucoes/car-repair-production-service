using Domain.Common;

namespace Application.Common.Messaging;

public static class DomainEventPublisherExtensions
{
    public static Task PublishDomainEventAsync<TDomainEvent>(
        this IEventPublisher eventPublisher,
        TDomainEvent domainEvent,
        Guid correlationId,
        CancellationToken cancellationToken = default)
        where TDomainEvent : class, IDomainEvent
    {
        var eventType = typeof(TDomainEvent).Name;
        var envelope = new MessageEnvelope<TDomainEvent>
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = correlationId.ToString("N"),
            SagaId = null,
            EventType = eventType,
            EventVersion = 1,
            OccurredAt = domainEvent.OccurredOnUtc,
            Message = domainEvent
        };

        return eventPublisher.PublishAsync(envelope, BuildRoutingKey(eventType), cancellationToken);
    }

    private static string BuildRoutingKey(string eventType)
    {
        var eventName = eventType.EndsWith("DomainEvent", StringComparison.Ordinal)
            ? eventType[..^"DomainEvent".Length]
            : eventType;

        var segments = new List<string>();
        var start = 0;

        for (var i = 1; i < eventName.Length; i++)
        {
            if (char.IsUpper(eventName[i]) && !char.IsUpper(eventName[i - 1]))
            {
                segments.Add(eventName[start..i].ToLowerInvariant());
                start = i;
            }
        }

        segments.Add(eventName[start..].ToLowerInvariant());
        return string.Join('.', segments);
    }
}

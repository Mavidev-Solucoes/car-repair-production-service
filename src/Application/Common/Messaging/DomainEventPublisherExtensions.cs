using System.Collections.Concurrent;
using System.Reflection;
using Domain.Common;

namespace Application.Common.Messaging;

public static class DomainEventPublisherExtensions
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> PublishDomainEventMethods = new();

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

    public static async Task PublishDomainEventsAsync(
        this IEventPublisher eventPublisher,
        IEnumerable<IDomainEvent> domainEvents,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var publishMethod = PublishDomainEventMethods.GetOrAdd(
                domainEvent.GetType(),
                static domainEventType => typeof(DomainEventPublisherExtensions)
                    .GetMethod(nameof(PublishDomainEventAsync), BindingFlags.Public | BindingFlags.Static)!
                    .MakeGenericMethod(domainEventType));

            var result = publishMethod.Invoke(null, [eventPublisher, domainEvent, correlationId, cancellationToken]);
            if (result is Task publishTask)
            {
                await publishTask;
            }
        }
    }

    private static string BuildRoutingKey(string eventType)
    {
        var eventName = eventType.EndsWith("DomainEvent", StringComparison.Ordinal)
            ? eventType[..^"DomainEvent".Length]
            : eventType;

        if (string.IsNullOrEmpty(eventName))
        {
            return string.Empty;
        }

        var segments = new List<string>();
        var start = 0;

        for (var i = 1; i < eventName.Length; i++)
        {
            var currentChar = eventName[i];
            var previousChar = eventName[i - 1];
            var nextChar = i + 1 < eventName.Length ? eventName[i + 1] : (char?)null;

            var isStartOfWord = char.IsUpper(currentChar) && char.IsLower(previousChar);
            var isEndOfAcronym =
                char.IsUpper(currentChar) &&
                char.IsUpper(previousChar) &&
                nextChar.HasValue &&
                char.IsLower(nextChar.Value);

            if (isStartOfWord || isEndOfAcronym)
            {
                segments.Add(eventName[start..i].ToLowerInvariant());
                start = i;
            }
        }

        segments.Add(eventName[start..].ToLowerInvariant());
        return string.Join('.', segments);
    }
}

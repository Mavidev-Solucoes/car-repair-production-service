namespace Application.Common.Messaging;

public sealed class MessageEnvelope<TMessage>
    where TMessage : class
{
    public required Guid MessageId { get; init; }

    public required string CorrelationId { get; init; }

    public string? SagaId { get; init; }

    public required string EventType { get; init; }

    public required int EventVersion { get; init; }

    public required DateTime OccurredAt { get; init; }

    public required TMessage Message { get; init; }
}

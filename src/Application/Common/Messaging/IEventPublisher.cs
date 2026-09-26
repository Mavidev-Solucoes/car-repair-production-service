namespace Application.Common.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(MessageEnvelope<TEvent> envelope, string routingKey, CancellationToken cancellationToken = default)
        where TEvent : class;
}

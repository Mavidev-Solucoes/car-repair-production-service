namespace Application.Common.Messaging;

public interface IEventPublisher
{
    Task PublishAsync(IntegrationMessage message, CancellationToken cancellationToken = default);
}

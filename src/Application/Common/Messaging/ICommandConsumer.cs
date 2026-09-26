namespace Application.Common.Messaging;

public interface ICommandConsumer
{
    Task SubscribeAsync<TCommand>(
        string queueName,
        string routingKey,
        Func<MessageEnvelope<TCommand>, CancellationToken, Task> onMessage,
        CancellationToken cancellationToken = default)
        where TCommand : class;
}
